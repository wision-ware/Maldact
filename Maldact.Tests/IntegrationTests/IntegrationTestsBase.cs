using System.Text.Json;
using Maldact.Backend.ML.Training.Data;
using Maldact.CLI.Commands.Config;
using Maldact.CLI.Commands.Config.Get;
using Maldact.CLI.Commands.Config.Set;
using Maldact.CLI.Commands.Connect;
using Maldact.CLI.Commands.Dataset;
using Maldact.CLI.Commands.Disconnect;
using Maldact.CLI.Commands.Model;
using Maldact.CLI.Commands.Results;
using Maldact.CLI.Commands.Server;
using Maldact.CLI.Commands.Status;
using Maldact.CLI.Commands.Stream;
using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Maldact.Core.Config.ConfigDefinitions;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Maldact.Tests.IntegrationTests;

/// <summary>
/// Base class providing an isolated environment on disk for CLI integration tests.
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    
    protected readonly PreprocessingContract _sampleContract;
    protected readonly DatasetManifest _sampleManifest;
    protected readonly DatasetManifest _sampleBakedManifest;
    protected readonly ModelSpecification _sampleModelSpec;
    protected readonly TrainingConfiguration _sampleTrainingConfig;
    
    protected string TestDirectory { get; }
    /// <summary>
    /// Gets the captured buffer representing standard output (stdout).
    /// </summary>
    protected TestConsole Out { get; }
    
    /// <summary>
    /// Gets the captured buffer representing standard error (stderr).
    /// </summary>
    protected TestConsole Error { get; }
    protected CommandApp App { get; }

    protected IntegrationTestBase()
    {
        TestDirectory = Path.Combine(Path.GetTempPath(), $"Maldact_Integration_{Guid.NewGuid():N}");
        Directory.CreateDirectory(TestDirectory);
        
        IndexingInvariantManager.OverrideSandboxPath(TestDirectory);
        
        Out = new TestConsole();
        Error = new TestConsole();
        
        // override static routing hub for test isolation
        Terminal.Out = Out;
        Terminal.Error = Error;
        AnsiConsole.Console = Out;
        
        App = new CommandApp(); 
        
        App.Configure(config =>
        {
            config.ConfigureConsole(Out);

            config.PropagateExceptions();

            config.AddBranch("server", server =>
            {
                server.AddCommand<ServerStartCommand>("start");
                server.AddCommand<ServerStopCommand>("stop");
                server.AddCommand<ServerStatusCommand>("status");
            });

            config.AddBranch("model", model =>
            {
                model.AddCommand<ModelTrainCommand>("train");
            });

            config.AddBranch("results", results =>
            {
                results.AddCommand<ResultsLatestCommand>("latest");
                results.AddCommand<ResultsGetCommand>("get");
                results.AddCommand<ResultsDeleteCommand>("delete");
                results.AddCommand<ResultsQueryCommand>("query");
                results.AddBranch("query", query =>
                {
                    query.AddCommand<ResultsQueryDeleteCommand>("delete");
                });
            });

            config.AddCommand<ConnectCommand>("connect");
            config.AddCommand<DisconnectCommand>("disconnect");
            config.AddCommand<StatusCommand>("status");
            config.AddCommand<StreamCommand>("stream");

            config.AddBranch("config", cfg =>
            {
                cfg.AddBranch("get", get =>
                {
                    get.AddCommand<ConfigGetModelSpecificationCommand>("model-specification");
                    get.AddCommand<ConfigGetPreprocessingContractCommand>("preprocessing-contract");
                    get.AddCommand<ConfigGetTrainingConfigurationCommand>("training-configuration");
                    get.AddCommand<ConfigGetServerConfigurationCommand>("server-configuration");
                });
                cfg.AddBranch("set", set =>
                {
                    set.AddCommand<ConfigSetModelSpecificationCommand>("model-specification");
                    set.AddCommand<ConfigSetPreprocessingContractCommand>("preprocessing-contract");
                    set.AddCommand<ConfigSetTrainingConfigurationCommand>("training-configuration");
                    set.AddCommand<ConfigSetServerConfigurationCommand>("server-configuration");
                });
                cfg.AddCommand<ConfigListCommand>("list");
            });

            config.AddBranch("dataset", dataset =>
            {
                dataset.AddCommand<DatasetProcessCommand>("process");
            });
        });
        
        // 1. Concrete Model Specification using your CNN topology layout
        _sampleModelSpec = new ModelSpecification
        {
            Name = "Integration_CNN_v1",
            Algorithm = ModelSpecification.AlgorithmType.Cnn,
            InputDimension = 2,   // Matches the feature count of your dataset stream
            OutputDimension = 2,  // Matches the number of target classes ("Idle", "Active")
            WindowSize = 10,
            WindowStride = 2,
            OverlapPoolingMethod = ModelSpecification.ResultOverlapTimePoolingMethod.Max,
            Cnn = new CnnParameters
            {
                ChannelSizes = new[] { 16 },
                KernelSize = 3,
                Stride = 1
            }
        };

        // 2. Concrete Training Configuration matching your properties exactly
        _sampleTrainingConfig = new TrainingConfiguration
        {
            Device = TrainingConfiguration.DeviceType.Cpu,
            ConsolidationAlgorithms = TrainingConfiguration.ConsolidationAlgorithm.BasicAttention,
            MaxEpochs = 50,          
            BatchesPerEpoch = 10,    
            LearningRate = 0.05,     // Very hot learning rate for instant overfitting
            Patience = 10,           // Don't early-stop before it figures out the 10.0f spike
            
            BatchSize = 1,            
            RandomSeed = 1337
        };

        // 3. Complete Dataset Manifest
        _sampleManifest = new DatasetManifest
        {
            DatasetName = "Integration_Mock_Dataset",
            GlobalSampleRateHz = 100.0,
            Classes = new[] { "Idle", "Active" },
            TrainingStreams = new List<StreamManifest>
            {
                new() 
                { 
                    FileName = "train_01.bin", 
                    DurationMs = 2000, 
                    Events = new List<EventManifest> { new() { ClassLabel = "Active", StartMs = 500, EndMs = 1500 } } 
                }
            },
            CrossValidationStreams = new List<StreamManifest>
            {
                new() 
                { 
                    FileName = "val_01.bin", 
                    DurationMs = 1000, 
                    Events = new List<EventManifest> { new() { ClassLabel = "Idle", StartMs = 0, EndMs = 1000 } } 
                }
            }
        };
    
        _sampleContract = new PreprocessingContract
        {
            InputDimension = 2,
            InputSampleRate = 100.0,
            Pipeline = new List<PreprocessingStep>
            {
                new() { Type = PreprocessingStep.PreprocessingStepType.Imputation, Imputation = new ImputationOptions { Method = ImputationOptions.ImputationMethod.ForwardFill } }
            },
            FinalReshaping = new ReshapingOptions { Method = ReshapingOptions.ReshapeMethod.Strict, TargetDimension = 2 }
        };
    
        _sampleBakedManifest = _sampleManifest with
        {
            Preprocessing = _sampleContract
        };
    }

    /// <summary>
    /// Helper to write a configuration model directly to disk as a JSON file.
    /// </summary>
    protected string WriteConfigFile<T>(string fileName, T data) where T : class
    {
        string filePath = Path.Combine(TestDirectory, fileName);
        string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
        return filePath;
    }

    /// <summary>
    /// Clean up filesystem traces to preserve environment hygiene.
    /// </summary>
    public virtual void Dispose()
    {
        Terminal.Out.Clear();
        Terminal.Error.Clear();

        if (Directory.Exists(TestDirectory))
        {
            try { Directory.Delete(TestDirectory, recursive: true); } catch { /* bypass active file locks */ }
        }
    }
}