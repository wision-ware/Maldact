using System.ComponentModel;
using System.Text.Json;
using Maldact.Backend.ML.Training.Data;
using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Maldact.Common.Configuration.JsonConfiguration;
using Maldact.Core.Config.ConfigDefinitions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Maldact.CLI.Commands.Dataset;

/// <summary>
/// Defines clean command-line options and thread pool parameters for dataset stream preprocessing requests.
/// </summary>
public sealed class DatasetProcessSettings : GlobalCommandSettings
{
    /// <summary>Gets or initializes the source directory path containing the raw target file components.</summary>
    [CommandArgument(0, "[datasetDirectory]")]
    [Description("Path to the directory containing raw data streams and the 'dataset.json' manifest.")]
    public string DatasetDirectory { get; init; } = string.Empty;

    /// <summary>Gets or initializes the optional path override targeting a localized preprocessing schema block.</summary>
    [CommandOption("-c|--contract <contractPath>")]
    [Description("Optional path to an overriding PreprocessingContract JSON file. If omitted, uses the active global configuration index.")]
    public string? ContractPath { get; init; }

    /// <summary>Gets or initializes the target output folder path where optimized tensor layers are written.</summary>
    [CommandOption("-o|--output <outputDirectory>")]
    [Description("Directory where baked tensor files will be saved. Defaults to a 'baked' folder inside the dataset directory.")]
    public string? OutputDirectory { get; init; }

    /// <summary>Gets or initializes the structural parallel processing throttling limits mapping available compute cores.</summary>
    [CommandOption("-p|--parallelism")]
    [Description("Maximum number of concurrent file streams to process simultaneously.")]
    public int MaxParallelism { get; init; } = Environment.ProcessorCount;
}

/// <summary>
/// Resolves configuration contracts and targets workspace files to execute safe parallel data preparation rules.
/// </summary>
public sealed class DatasetProcessCommand : AsyncCommand<DatasetProcessSettings>
{
    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, DatasetProcessSettings settings, CancellationToken ct)
    {
        string sourceDir = Path.GetFullPath(settings.DatasetDirectory);
        string targetDir = settings.OutputDirectory ?? Path.Combine(sourceDir, "processed");

        PreprocessingContract? contract = null;

        if (!string.IsNullOrWhiteSpace(settings.ContractPath))
        {
            if (!File.Exists(settings.ContractPath))
            {
                Terminal.Error.MarkupLine($"[bold red]File error:[/] Specified preprocessing contract override file missing: [yellow]{settings.ContractPath}[/]");
                return 1;
            }
            
            var provider = new JsonConfigurationProvider<PreprocessingContract>(settings.ContractPath);
            contract = provider.Config;
        }
        else
        {   
            var provider = GlobalConfigurationIndexManager.GetPreprocessingContractProvider();
            contract = provider?.Config;
        }

        if (contract == null)
        {
            Terminal.Error.MarkupLine($"[bold red]Error:[/] No processing parameters specified. Provide a local contract " +
                                      $"path using [grey]--contract[/] or run the configuration command to register a global contract state first.");
            return 1;
        }

        await Terminal.Error.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green bold"))
            .StartAsync($"Processing dataset files using multi-threaded execution pools (Threads: {settings.MaxParallelism})...", async ctx =>
            {
                var orchestrator = new DatasetBaker();
                await orchestrator.ProcessDatasetAsync(sourceDir, targetDir, contract, settings.MaxParallelism);
            });

        Terminal.Out.MarkupLine("[bold green]✔ Dataset compilation completed successfully.[/]");
        Terminal.Out.MarkupLine($"[grey]Outputs written to target environment folder:[/] [white]{targetDir}[/]");

        return 0;
    }
}