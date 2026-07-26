using System.Runtime.InteropServices;
using System.Text.Json;
using FluentAssertions;
using Maldact.Backend.ML.Training.Data;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.ML.Training;
using Moq;

namespace Maldact.Tests.BackendTests.MLTests.Integration.DataLoading;

public class DataLoaderIntegrationTests
{
    private class TempDirectory : IDisposable
    {
        public string DirectoryPath { get; }

        public TempDirectory()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(DirectoryPath);
        }

        public void WriteBinary(string fileName, float[] data)
        {
            var bytes = MemoryMarshal.AsBytes(data.AsSpan()).ToArray();
            File.WriteAllBytes(Path.Combine(DirectoryPath, fileName), bytes);
        }

        public void WriteText(string fileName, string content)
        {
            File.WriteAllText(Path.Combine(DirectoryPath, fileName), content);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true);
        }
    }
    
    private static void BuildMockDataset(TempDirectory dir)
    {
        // 2 features, 2 timesteps per file
        float[] trainData = { 1f, 2f, 3f, 4f };
        float[] valData = { 5f, 6f, 7f, 8f };

        dir.WriteBinary("train.bin", trainData);
        dir.WriteBinary("val.bin", valData);

        var manifest = new DatasetManifest
        {
            DatasetName = "Test",
            GlobalSampleRateHz = 100, // 10ms per frame
            Classes = new[] { "Dog" },
            Preprocessing = new PreprocessingContract
            {
                InputDimension = 2,
                InputSampleRate = 100,
                FinalReshaping = new ReshapingOptions { TargetDimension = 2, Method = ReshapingOptions.ReshapeMethod.Strict },
                Pipeline = new List<PreprocessingStep>
                {
                    new() { Type = PreprocessingStep.PreprocessingStepType.Imputation, Imputation = new ImputationOptions { Method = ImputationOptions.ImputationMethod.ZeroFill } }
                }
            },
            TrainingStreams = new List<StreamManifest>
            {
                new() { FileName = "train.bin", DurationMs = 20 }
            },
            CrossValidationStreams = new List<StreamManifest>
            {
                new() 
                { 
                    FileName = "val.bin", 
                    DurationMs = 20,
                    Events = new List<EventManifest>
                    {
                        new() { ClassLabel = "Dog", StartMs = 10, EndMs = 20 }
                    }
                }
            }
        };

        dir.WriteText(DatasetBaker.BakedManifestName, JsonSerializer.Serialize(manifest, DatasetJsonContext.Default.DatasetManifest));
    }
    
    [Fact]
    public async Task GenerateBatches_WithIdenticalSeeds_YieldsStrictlyDeterministicTensors()
    {
        // arrange
        using var dir = new TempDirectory();
        BuildMockDataset(dir);

        int seed = 42;
        var windowDuration = TimeSpan.FromMilliseconds(10);
        var stride = TimeSpan.FromMilliseconds(10);

        // mock formatter to isolate loader logic (just pass features through)
        var mockFormatter = new Mock<ILabeledBatchFormatter>();
        mockFormatter.Setup(f => f.Format(It.IsAny<IReadOnlyList<RawTrainingWindow>>()))
            .Returns((IReadOnlyList<RawTrainingWindow> windows) => new FormattedLabeledBatch
            {
                FlattenedFeatures = windows.SelectMany(w => w.Features.SelectMany(f => f)).ToArray(),
                FeatureShape = [],
                FlattenedTargets = [],
                TargetShape = []
            });

        // act - run pipeline A
        using var datasetA = await ManifestDataset.LoadAsync(dir.DirectoryPath, rngSeed: seed);
        var loaderA = new StreamingDataLoader(
            datasetA, mockFormatter.Object, batchSize: 4, batchesPerEpoch: 10, stride, windowDuration, seed: seed);
        var batchesA = loaderA.GenerateBatches().ToList();

        // act - run pipeline B
        using var datasetB = await ManifestDataset.LoadAsync(dir.DirectoryPath, rngSeed: seed);
        var loaderB = new StreamingDataLoader(
            datasetB, mockFormatter.Object, batchSize: 4, batchesPerEpoch: 10, stride, windowDuration, seed: seed);
        var batchesB = loaderB.GenerateBatches().ToList();

        // assert
        batchesA.Should().HaveCount(10);
        batchesB.Should().HaveCount(10);

        // verify exact sequence match across all sampled bytes
        for (int i = 0; i < batchesA.Count; i++)
        {
            batchesA[i].FlattenedFeatures.Should().BeEquivalentTo(
                batchesB[i].FlattenedFeatures, 
                options => options.WithStrictOrdering(),
                $"Batch {i} should be mathematically identical across deterministic runs"
            );
        }
    }
}