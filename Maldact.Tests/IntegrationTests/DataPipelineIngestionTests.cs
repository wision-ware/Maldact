using System.IO.Compression;
using System.Text.Json;
using FluentAssertions;
using Maldact.Backend.ML.Training.Data;
using Maldact.CLI.Commands.Stream;
using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Maldact.Client.Networking;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.Server.Authentication;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Spectre.Console.Testing;

namespace Maldact.Tests.IntegrationTests;

[Collection("Integration")]
public sealed class DataPipelineIntegrationTests : IntegrationTestBase
{

    [Fact]
    public async Task DatasetProcessCommand_WithValidManifestAndContract_SuccessfullyBakesDataset()
    {
        // ARRANGE: Scaffold the user workspace
        string datasetDir = Path.Combine(TestDirectory, "MockDataset");
        Directory.CreateDirectory(datasetDir);

        // Write the DatasetManifest (dataset.json)
        string manifestPath = Path.Combine(datasetDir, "dataset.json");
        string manifestJson = JsonSerializer.Serialize(_sampleManifest, DatasetJsonContext.Default.DatasetManifest);
        await File.WriteAllTextAsync(manifestPath, manifestJson);

        // Write the PreprocessingContract to a separate file so we can pass it via the -c flag
        string contractPath = Path.Combine(datasetDir, "pipeline-rules.json");
        string contractJson = JsonSerializer.Serialize(_sampleContract);
        await File.WriteAllTextAsync(contractPath, contractJson);

        // Generate dummy binary files to satisfy the file I/O checks during baking
        // Using a tiny 256-byte buffer just to give the baker something to open and read
        byte[] dummyBinaryData = new byte[256]; 
        await File.WriteAllBytesAsync(Path.Combine(datasetDir, "train_01.bin"), dummyBinaryData);
        await File.WriteAllBytesAsync(Path.Combine(datasetDir, "val_01.bin"), dummyBinaryData);

        string expectedOutputDir = Path.Combine(datasetDir, "processed");
        
        this.Out.Profile.Width = 300;
        
        // ACT: Execute the command just like a user would in the CLI
        int exitCode = await App.RunAsync(new[] 
        { 
            "dataset", "process", 
            datasetDir, 
            "--contract", contractPath,
            "--parallelism", "2" // Restrict threads to keep the test environment stable
        });

        // ASSERT
        exitCode.Should().Be(0, "the dataset processor must complete without throwing errors");
        
        this.Out.Output.Should().Contain("Dataset compilation completed successfully");
        this.Out.Output.Should().Contain(expectedOutputDir);

        // Verify the baker actually created the target directory structure
        Directory.Exists(expectedOutputDir).Should().BeTrue("the baker must physically create the output directory");
        
        // Optionally: Assert that specific baked tensor files exist, depending on what your DatasetBaker outputs
        // File.Exists(Path.Combine(expectedOutputDir, "train_01.tensor")).Should().BeTrue();
    }
    
    [Fact]
    public async Task ModelTrainCommand_WithValidConfigurations_SuccessfullyAssemblesDeploymentBundle()
    {
        // ARRANGE: Scaffold physical directory boundaries
        string workspaceDir = Path.Combine(TestDirectory, "TrainingWorkspace");
        string datasetSrcDir = Path.Combine(workspaceDir, "DatasetSource");
        Directory.CreateDirectory(datasetSrcDir);

        this.Out.Profile.Width = 300; // Shield from word-wrap newlines breaking absolute path strings

        // Write parameter configuration files to the workspace root
        string trainConfigPath = Path.Combine(workspaceDir, "training.json");
        await File.WriteAllTextAsync(trainConfigPath, JsonSerializer.Serialize(_sampleTrainingConfig));

        string modelSpecPath = Path.Combine(workspaceDir, "model.json");
        await File.WriteAllTextAsync(modelSpecPath, JsonSerializer.Serialize(_sampleModelSpec));

        // Write layout artifacts required by ManifestDataset.LoadAsync inside the source directory
        string manifestPath = Path.Combine(datasetSrcDir, DatasetBaker.BakedManifestName);
        string manifestJson = JsonSerializer.Serialize(_sampleBakedManifest, DatasetJsonContext.Default.DatasetManifest);
        await File.WriteAllTextAsync(manifestPath, manifestJson);

        string contractPath = Path.Combine(datasetSrcDir, "contract.json");
        await File.WriteAllTextAsync(contractPath, JsonSerializer.Serialize(_sampleContract));

        // Write mock raw binary files to prevent IO file-not-found exceptions
        byte[] dummyBytes = new byte[8192];
        
        await File.WriteAllBytesAsync(Path.Combine(datasetSrcDir, "train_01.bin"), dummyBytes);
        await File.WriteAllBytesAsync(Path.Combine(datasetSrcDir, "val_01.bin"), dummyBytes);

        string outputModelZipPath = Path.Combine(workspaceDir, "final_deployment_artifact.zip");

        // ACT: Drive the end-to-end orchestration command 
        int exitCode = await App.RunAsync(new[]
        {
            "model", "train",
            datasetSrcDir,
            outputModelZipPath,
            "--train-config", trainConfigPath,
            "--model-spec", modelSpecPath
        });

        // ASSERT: Validate the core framework execution rules
        exitCode.Should().Be(0, "the compilation execution tree must terminate cleanly without throwing runtime errors");
        this.Out.Output.Should().Contain("Training Sequence Finalized!");

        // Physical File Verification
        File.Exists(outputModelZipPath).Should().BeTrue("the system must explicitly build the deployment zip asset on disk");

        // Inspect compiled bundle contents to guarantee data integrity preservation
        using var archive = ZipFile.OpenRead(outputModelZipPath);
        archive.GetEntry("model_spec.json").Should().NotBeNull("the calibration matrix must embed a copy of the target Model Specification");
        archive.GetEntry("preprocessing_contract.json").Should().NotBeNull("the calibration matrix must embed a copy of the Preprocessing Contract");
    }
    
    [Fact]
    public async Task LiveInference_WhenStreamingActiveData_ProcessesFramesAndStoresResults()
    {
        // ==========================================
        // PHASE 1: ARRANGE WORKSPACE & TRAIN MODEL
        // ==========================================
        string workspaceDir = Path.Combine(TestDirectory, "StreamingWorkspace");
        string datasetSrcDir = Path.Combine(workspaceDir, "DatasetSource");
        Directory.CreateDirectory(datasetSrcDir);
        this.Out.Profile.Width = 300;

        string trainConfigPath = Path.Combine(workspaceDir, "training.json");
        await File.WriteAllTextAsync(trainConfigPath, JsonSerializer.Serialize(_sampleTrainingConfig));

        string modelSpecPath = Path.Combine(workspaceDir, "model.json");
        await File.WriteAllTextAsync(modelSpecPath, JsonSerializer.Serialize(_sampleModelSpec));

        string manifestPath = Path.Combine(datasetSrcDir, DatasetBaker.BakedManifestName);
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(_sampleBakedManifest, 
            DatasetJsonContext.Default.DatasetManifest));

        string contractPath = Path.Combine(datasetSrcDir, "contract.json");
        await File.WriteAllTextAsync(contractPath, JsonSerializer.Serialize(_sampleContract));

        // THE ML HACK: Create a mathematically unmistakable dataset
        byte[] dummyTrainBytes = new byte[8192];
        byte[] dummyValBytes = new byte[8192]; // completely zeros -> "Idle"

        // In _sampleManifest, "Active" is from 500ms to 1500ms.
        // At 100Hz, 1 frame = 2 features = 8 bytes. 500ms starts at frame 50 (byte 400). 1500ms ends at frame 150 (byte 1200).
        // Inject a massive 10.0f signal precisely into the "Active" window.
        for (int i = 400; i < 1200; i += 4)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(10.0f), 0, dummyTrainBytes, i, 4);
        }

        await File.WriteAllBytesAsync(Path.Combine(datasetSrcDir, "train_01.bin"), dummyTrainBytes);
        await File.WriteAllBytesAsync(Path.Combine(datasetSrcDir, "val_01.bin"), dummyValBytes);

        string outputModelZipPath = Path.Combine(workspaceDir, "deployment_artifact.zip");

        int trainExitCode = await App.RunAsync(new[]
        {
            "model", "train",
            datasetSrcDir,
            outputModelZipPath,
            "--train-config", trainConfigPath,
            "--model-spec", modelSpecPath
        });
        
        trainExitCode.Should().Be(0, "Setup Failure: Could not generate a valid deployment artifact.");

        // ==========================================
        // PHASE 2: BOOT SERVER
        // ==========================================
        var serverConfig = new ServerConfiguration
        {
            ServerId = "integration-node", 
            AdminKeys = new() { "admin-token-xyz" },
            UserKeys = new() { "user-token-abc" }
        };
        string serverConfigPath = WriteConfigFile("serverConfig.json", serverConfig);

        using var serverCts = new CancellationTokenSource();
        var serverTask = Task.Run(async () =>
        {
            await App.RunAsync(new[] 
            { 
                "server", "start", 
                "5500", "5501", 
                outputModelZipPath, 
                "--config", serverConfigPath 
            }, serverCts.Token);
        });

        await Task.Delay(1000); // Give the TCP listeners and Torch engine time to boot

        try
        {
            // ==========================================
            // PHASE 3: STREAM DATA
            // ==========================================
            GlobalConnectionStateIndexManager.SetNewState(new ConnectionStateIndex
            {
                Host = "127.0.0.1",
                Port = 5500,
                AuthToken = new AuthToken("admin-token-xyz")
            });
            await App.RunAsync(new[] { "config", "set", "model-specification", modelSpecPath });
            // ControlClient.ValidatorOverride = (sender, cert, chain, errors) => true;
            
            using var simulatedStdin = new MemoryStream(dummyTrainBytes);
            StreamCommand.GetStandardInputStream = () => simulatedStdin;

            int streamExitCode = await App.RunAsync(new[] { "stream" });
            streamExitCode.Should().Be(0, "the streaming client must cleanly ingest the MemoryStream and disconnect");

            // Give the inference engine time to chew through the 16 chunks
            await Task.Delay(1000);

            // ==========================================
            // PHASE 4: QUERY RESULTS
            // ==========================================
            var resultsConsole = new TestConsole();
            resultsConsole.Profile.Width = 300;
            
            // To ensure we catch errors if the payload is empty
            Terminal.Out = resultsConsole;
            Terminal.Error = resultsConsole; 
            Spectre.Console.AnsiConsole.Console = resultsConsole;

            // Request the results based on your command structure
            int queryExitCode = await App.RunAsync(new[] { "results", "query" });
            
            queryExitCode.Should().Be(0, "the results query command must execute without networking errors");

            string output = resultsConsole.Output;
            output.Should().NotBeNullOrWhiteSpace();
            
            // Assert that the server actually found our "Active" event, NOT an empty response!
            output.Should().NotContain("Empty response", "the ML engine should have positively identified the 10.0f signal");
            output.Should().Contain("Active", "the output JSON or table must contain the predicted 'Active' class label");
        }
        finally
        {
            if (Terminal.Error is TestConsole currentConsole)
            {
                currentConsole.Input.PushKey(ConsoleKey.Y);
                currentConsole.Input.PushKey(ConsoleKey.Enter);
            }

            await serverCts.CancelAsync();
            await serverTask;
            
            // ControlClient.ValidatorOverride = null;
            StreamCommand.GetStandardInputStream = Console.OpenStandardInput; 
            Terminal.Out = this.Out;
            Terminal.Error = this.Error;
            Spectre.Console.AnsiConsole.Console = this.Out;
        }
    }
}