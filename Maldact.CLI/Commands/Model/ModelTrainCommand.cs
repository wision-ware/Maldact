using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Maldact.Backend.ML.Builders;
using Maldact.Backend.ML.Consolidation.Tuning;
using Maldact.Backend.ML.Packaging;
using Maldact.Backend.ML.Training.Data;
using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Maldact.Common.Configuration.Extensions;
using Maldact.Common.Configuration.JsonConfiguration;
using Maldact.Core.Config;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.Data;
using Maldact.Core.ML.Consolidation.Tuning;
using Maldact.Core.ML.Training;

namespace Maldact.CLI.Commands.Model;

/// <summary>
/// Defines the options and path configurations for executing iterative deep neural network parameters calibration runs.
/// </summary>
public sealed class ModelTrainSettings : GlobalCommandSettings
{
    /// <summary>Gets or initializes the source path pointing to the workspace dataset archive target.</summary>
    [CommandArgument(0, "[datasetDir]")]
    public string Dataset { get; init; } = string.Empty;

    /// <summary>Gets or initializes the target output deployment package filesystem path destination.</summary>
    [CommandArgument(1, "[modelZip]")]
    public string Output { get; init; } = string.Empty;
    
    /// <summary>Gets or initializes the training parameter profile override rule path reference.</summary>
    [CommandOption("--train-config")]
    public string? TrainingConfigurationPath { get; init; }
    
    /// <summary>Gets or initializes the neural topology specification layer override path reference.</summary>
    [CommandOption("--model-spec")]
    public string? ModelSpecificationPath { get; init; }
    
    /// <summary>A log file path for optional training logs.</summary>
    [CommandOption("--metrics-log")]
    public string? MetricsLogPath { get; init; }
}

/// <summary>
/// Drives the end-to-end model training lifecycle including hardware context binding, loop optimization, and compressed artifact assembly.
/// </summary>
public sealed class ModelTrainCommand : AsyncCommand<ModelTrainSettings>
{
    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, ModelTrainSettings settings, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(settings.Dataset))
        {
            Terminal.Error.MarkupLine("[bold red]Argument error:[/] Dataset directory path not provided.");
            return 1;
        }
        if (string.IsNullOrEmpty(settings.Output))
        {
            Terminal.Error.MarkupLine("[bold red]Argument error:[/] Output zip path not provided.");
            return 1;
        }
        
        Terminal.Error.Write(new Rule("[grey]Maldact Model Pipeline[/]").LeftJustified());

        PipelineState data;
        try
        {
            data = await Terminal.Error.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .StartAsync("Initializing pipeline configurations...", async ctx =>
                {
                    var trainingConfigProvider = settings.TrainingConfigurationPath is null
                        ? GlobalConfigurationIndexManager.GetTrainingConfigurationProvider() ??
                          throw new InvalidDataException(
                              "Active Training Configuration is missing from index registry.")
                        : new JsonConfigurationProvider<TrainingConfiguration>(settings.TrainingConfigurationPath);

                    var modelSpecificationProvider = settings.ModelSpecificationPath is null
                        ? GlobalConfigurationIndexManager.GetModelSpecificationProvider() ??
                          throw new InvalidDataException(
                              "Active Model Specification is missing from index registry.")
                        : new JsonConfigurationProvider<ModelSpecification>(settings.ModelSpecificationPath);
                    
                    var trainingConfiguration = trainingConfigProvider.Config;
                    var modelSpecification = modelSpecificationProvider.Config;
                    var seed = trainingConfiguration.RandomSeed;

                    ctx.Status($"Loading dataset manifest from [white]{settings.Dataset}[/]...");
                    var rawLoader = await ManifestDataset.LoadAsync(settings.Dataset, seed);

                    ctx.Status("Setting up data loader...");
                    var formattedLoader = rawLoader.AddFormatting(modelSpecification, trainingConfiguration, seed);

                    return new PipelineState(rawLoader, formattedLoader, trainingConfiguration, modelSpecification);
                });
        }
        catch (Exception e)
        {
            Terminal.Error.MarkupLine($"[bold red]Configuration Error:[/] {e.Message}");
            return 1;
        }

        var metricsPayload = new MetricsExportPayload();
        var logLock = new object();

        Terminal.Out.MarkupLine("[bold green]✔[/] Core training pipeline workspace initialized.");
        Terminal.Error.MarkupLine("\n[grey]Starting Model Optimization...[/]");
        
        var trainingLoop = data.TrainingConfiguration.BuildLoop(data.ModelSpecification);
        IModelParameters? modelParams = null;

        await Terminal.Error.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn { Alignment = Justify.Left },
                new ProgressBarColumn(),
                new PercentageColumn(),
                new ElapsedTimeColumn(),
                new SpinnerColumn()
            )
            .StartAsync(async ctx =>
            {
                int maxEpochs = data.TrainingConfiguration.MaxEpochs ?? int.MaxValue;
                var progressTask = ctx.AddTask("[green]Allocating engine execution context...[/]", maxValue: maxEpochs);

                var progressReporter = new Progress<EpochMetrics>(metrics =>
                {
                    lock (logLock) { metricsPayload.TrainingMetrics.Add(metrics); }
                    
                    progressTask.Value = metrics.Epoch;
                    progressTask.Description = 
                        $"[green]Training[/] | " +
                        $"Epoch: [bold]{metrics.Epoch}/{metrics.MaxEpochs}[/] | " +
                        $"TrainLoss: [white]{metrics.TrainLoss:F4}[/] | " +
                        $"ValLoss: [white]{metrics.ValLoss:F4}[/] | " +
                        $"Patience: {metrics.PatienceElapsed}/{metrics.MaxPatience}";
                });

                modelParams = await trainingLoop.RunAsync(data.FormattedLoader, progressReporter);
                progressTask.Description = "[bold green]✔ Model parameters converged[/]";
                progressTask.StopTask();
            });
        
        if (modelParams == null) throw new InvalidDataException("Training process unexpectedly yielded no parameters.");
        
        var rawInference = modelParams.GetRawInference(data.ModelSpecification);
        double frameRate = data.ModelSpecification.GetFrameRate(data.RawLoader.SampleRateHz);
        var consolidatorOptimizer = data.TrainingConfiguration.GetOptimizer(frameRate, data.RawLoader.Classes);
        var tuningLoop = new ConsolidatorTuningLoop(rawInference, data.TrainingConfiguration.MaxTuningCycles);
        
        ConsolidatorConfiguration? consolidatorConfiguration = null;
        
        await Terminal.Error.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn { Alignment = Justify.Left },
                new ProgressBarColumn(),
                new PercentageColumn(),
                new ElapsedTimeColumn(),
                new SpinnerColumn()
            )
            .StartAsync(async ctx =>
            {
                int maxCycles = data.TrainingConfiguration.MaxTuningCycles;
                var tuneTask = ctx.AddTask("[green]Preparing tuning loop...[/]", maxValue: maxCycles);

                var tuneReporter = new Progress<TuningMetrics>(metrics =>
                {
                    lock (logLock) { metricsPayload.TuningMetrics.Add(metrics); }
                    
                    tuneTask.Value = metrics.CurrentIteration;
                    tuneTask.Description = 
                        $"[green]Tuning[/] | " +
                        $"Step: [white]{metrics.CurrentIteration}/{metrics.MaxIterations}[/] | " +
                        $"Latest F1: [white]{metrics.LatestScore:P2}[/] | " +
                        $"Best: [white]{metrics.BestAlgorithm}[/] ({metrics.BestScore:P2})";
                });

                consolidatorConfiguration = await tuningLoop.RunAsync(data.RawLoader, consolidatorOptimizer, tuneReporter);
                tuneTask.Description = $"[bold green]✔ Tuning phase complete. Algorithm: {consolidatorConfiguration.GetType().Name.Replace("Configuration", "")}[/]";
                tuneTask.StopTask();
            });
        
        if (consolidatorConfiguration == null)
        {
            Terminal.Error.MarkupLine("[bold red]Tuning Error: Consolidator tuning terminated without finalized parameters.[/]");
            return 1;
        }

        await Terminal.Error.Status()
            .Spinner(Spinner.Known.BouncingBar)
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync("Assembling compressed deployment artifact...", async ctx =>
            {
                var deploymentArtifact = new DeploymentArtifact
                {
                    ConsolidatorConfiguration = consolidatorConfiguration,
                    Parameters = modelParams,
                    Contract = data.RawLoader.Preprocessing ?? throw new InvalidOperationException("The pipeline environment lacks a valid preprocessing data contract reference."),
                    Specification = data.ModelSpecification
                };
                
                ctx.Status($"Writing deployment archive to target: [yellow]{settings.Output}[/]...");
                await ArtifactManager.CreateDeploymentZipAsync(deploymentArtifact, settings.Output);
            });

        Terminal.Out.MarkupLine($"\n[bold green]✔ Training Sequence Finalized![/] Deployment artifact saved successfully to: [white]{settings.Output}[/]");
        
        if (!string.IsNullOrWhiteSpace(settings.MetricsLogPath))
        {
            var jsonOptions = new JsonSerializerOptions 
            { 
                WriteIndented = true, 
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } 
            };
            string jsonOutput = JsonSerializer.Serialize(metricsPayload, jsonOptions);
            await File.WriteAllTextAsync(settings.MetricsLogPath, jsonOutput, ct);
            Terminal.Out.MarkupLine($"[grey]Metrics saved to: {settings.MetricsLogPath}[/]");
        }
        return 0;
    }
    
    private sealed record PipelineState(
        ManifestDataset RawLoader,
        IFormattedLabeledTrainingDataLoader FormattedLoader,
        TrainingConfiguration TrainingConfiguration,
        ModelSpecification ModelSpecification);
    
    private sealed record MetricsExportPayload
    {
        public List<EpochMetrics> TrainingMetrics { get; } = new();
        public List<TuningMetrics> TuningMetrics { get; } = new();
    }
}
