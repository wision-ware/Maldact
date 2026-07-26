using System.IO.Compression;
using System.Text.Json;
using FluentAssertions;
using Maldact.Backend.ML.Training.Data;
using Maldact.Client.Indexing;
using Maldact.Core.Config.ConfigDefinitions;

namespace Maldact.Tests.IntegrationTests;

[Collection("Integration")]
public sealed class TrainingDeterminismIntegrationTests : IntegrationTestBase
{
    // Assume _sampleTrainingConfig, _sampleManifest, and _sampleContract 
    // are available from your constructor setup!
    
    [Theory]
    [InlineData(ModelSpecification.AlgorithmType.Cnn)]
    [InlineData(ModelSpecification.AlgorithmType.Gru)]
    [InlineData(ModelSpecification.AlgorithmType.TreeEnsemble)]
    public async Task ModelTrainCommand_WithFixedSeed_ProducesDeterministicArtifacts(ModelSpecification.AlgorithmType algorithmType)
    {
        // ==========================================
        // ARRANGE: SETUP CONFIGS & DATA
        // ==========================================
        string workspaceDir = Path.Combine(TestDirectory, $"Determinism_{algorithmType}");
        string datasetSrcDir = Path.Combine(workspaceDir, "DatasetSource");
        Directory.CreateDirectory(datasetSrcDir);
        this.Out.Profile.Width = 300;

        // Force a strict random seed and keep training short
        var deterministicConfig = _sampleTrainingConfig with 
        { 
            RandomSeed = 42,
            MaxEpochs = 2, 
            BatchSize = 5,
            BatchesPerEpoch = 10,
            Device = TrainingConfiguration.DeviceType.Cpu // Enforce CPU (CUDA can sometimes introduce non-determinism)
        };
        
        string trainConfigPath = Path.Combine(workspaceDir, "training.json");
        await File.WriteAllTextAsync(trainConfigPath, JsonSerializer.Serialize(deterministicConfig));

        // Dynamically build the requested model topology
        var modelSpec = new ModelSpecification
        {
            Name = $"Determinism_Test_{algorithmType}",
            Algorithm = algorithmType,
            InputDimension = 2,
            OutputDimension = 2,
            WindowSize = 10,
            WindowStride = 2,
            OverlapPoolingMethod = ModelSpecification.ResultOverlapTimePoolingMethod.Max,
            Cnn = algorithmType == ModelSpecification.AlgorithmType.Cnn ? new CnnParameters { ChannelSizes = new[] { 16 }, KernelSize = 3, Stride = 1 } : null,
            Gru = algorithmType == ModelSpecification.AlgorithmType.Gru ? new GruParameters { HiddenSize = 16, NumLayers = 1, Dropout = 0.0 } : null,
            Tree = algorithmType == ModelSpecification.AlgorithmType.TreeEnsemble ? new TreeParameters { EnsembleType = TreeParameters.TreeType.RandomForest, NumberOfTrees = 10, MaxDepth = 3 } : null
        };

        string modelSpecPath = Path.Combine(workspaceDir, "model.json");
        await File.WriteAllTextAsync(modelSpecPath, JsonSerializer.Serialize(modelSpec));

        // Write Manifest and Contract
        await File.WriteAllTextAsync(
            Path.Combine(datasetSrcDir, DatasetBaker.BakedManifestName), 
            JsonSerializer.Serialize(_sampleBakedManifest, DatasetJsonContext.Default.DatasetManifest)
            );
        await File.WriteAllTextAsync(Path.Combine(datasetSrcDir, "contract.json"), JsonSerializer.Serialize(_sampleContract));

        // Generate static dummy data
        byte[] dummyTrainBytes = new byte[8192];
        byte[] dummyValBytes = new byte[8192]; // Leave validation as zeros

        for (int i = 400; i < 1200; i += 4)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(10.0f), 0, dummyTrainBytes, i, 4);
        }

        await File.WriteAllBytesAsync(Path.Combine(datasetSrcDir, "train_01.bin"), dummyTrainBytes);
        await File.WriteAllBytesAsync(Path.Combine(datasetSrcDir, "val_01.bin"), dummyValBytes);

        string outputZip1 = Path.Combine(workspaceDir, "artifact_run_1.zip");
        string outputZip2 = Path.Combine(workspaceDir, "artifact_run_2.zip");

        // ==========================================
        // ACT: RUN TRAINING TWICE
        // ==========================================
        
        int exitCode1 = await App.RunAsync(new[]
        {
            "model", "train", datasetSrcDir, outputZip1, 
            "--train-config", trainConfigPath, "--model-spec", modelSpecPath
        });
        
        // Reset global states just in case
        // GlobalConfigurationIndexManager.ResetForTesting(); 

        int exitCode2 = await App.RunAsync(new[]
        {
            "model", "train", datasetSrcDir, outputZip2, 
            "--train-config", trainConfigPath, "--model-spec", modelSpecPath
        });

        // ==========================================
        // ASSERT: BYTE-FOR-BYTE COMPARISON
        // ==========================================
        exitCode1.Should().Be(0, "Run 1 must complete successfully");
        exitCode2.Should().Be(0, "Run 2 must complete successfully");
        
        File.Exists(outputZip1).Should().BeTrue();
        File.Exists(outputZip2).Should().BeTrue();

        using var archive1 = ZipFile.OpenRead(outputZip1);
        using var archive2 = ZipFile.OpenRead(outputZip2);

        // Ensure both ZIPs generated the exact same number of files (e.g., parameters.bin, model.json)
        archive1.Entries.Count.Should().Be(archive2.Entries.Count, "Both deployments should contain the same number of files");

        foreach (var entry1 in archive1.Entries)
        {
            var entry2 = archive2.GetEntry(entry1.FullName);
            entry2.Should().NotBeNull($"Run 2 is missing file: {entry1.FullName}");

            using var stream1 = entry1.Open();
            using var stream2 = entry2.Open();

            using var ms1 = new MemoryStream();
            using var ms2 = new MemoryStream();

            await stream1.CopyToAsync(ms1);
            await stream2.CopyToAsync(ms2);

            byte[] bytes1 = ms1.ToArray();
            byte[] bytes2 = ms2.ToArray();

            if (algorithmType == ModelSpecification.AlgorithmType.TreeEnsemble && 
                (entry1.FullName.Contains("weights.bin") || entry1.FullName.EndsWith(".zip")))
            {
                using var innerZip1 = new ZipArchive(new MemoryStream(bytes1), ZipArchiveMode.Read);
                using var innerZip2 = new ZipArchive(new MemoryStream(bytes2), ZipArchiveMode.Read);

                innerZip1.Entries.Count.Should().Be(innerZip2.Entries.Count, 
                    $"Model architecture mismatch in {entry1.FullName}");

                foreach (var innerEntry1 in innerZip1.Entries)
                {
                    var innerEntry2 = innerZip2.GetEntry(innerEntry1.FullName);
                    innerEntry2.Should().NotBeNull();

                    using var innerStream1 = innerEntry1.Open();
                    using var innerStream2 = innerEntry2.Open();
                    using var innerMs1 = new MemoryStream();
                    using var innerMs2 = new MemoryStream();
                    
                    await innerStream1.CopyToAsync(innerMs1);
                    await innerStream2.CopyToAsync(innerMs2);

                    innerMs1.ToArray().SequenceEqual(innerMs2.ToArray()).Should().BeTrue(
                        $"Algorithm {algorithmType} failed determinism check on internal model file '{innerEntry1.FullName}'.");
                }
            }
            else
            {
                // Standard byte-for-byte comparison for Torch weights, JSON configs, and manifests
                bytes1.SequenceEqual(bytes2).Should().BeTrue(
                    $"Algorithm {algorithmType} failed determinism check on file '{entry1.FullName}'.");
            }
        }
    }
}