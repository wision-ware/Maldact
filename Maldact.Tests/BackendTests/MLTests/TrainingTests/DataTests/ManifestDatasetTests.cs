using System.Runtime.InteropServices;
using System.Text.Json;
using FluentAssertions;
using Maldact.Backend.ML.Training.Data;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.Data;

namespace Maldact.Tests.BackendTests.MLTests.TrainingTests.DataTests;

public class ManifestDatasetTests
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
    public async Task LoadAsync_MissingManifest_ThrowsFileNotFound()
    {
        using var dir = new TempDirectory();

        Func<Task> act = async () => await ManifestDataset.LoadAsync(dir.DirectoryPath, rngSeed: 42);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task LoadAsync_ValidSetup_InitializesStreamsAndClasses()
    {
        using var dir = new TempDirectory();
        BuildMockDataset(dir);

        using var dataset = await ManifestDataset.LoadAsync(dir.DirectoryPath, rngSeed: 42);

        dataset.SampleRateHz.Should().Be(100);
        dataset.Classes.Should().ContainSingle(c => c.ClassName == "Dog");
        dataset.Preprocessing.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRandomTrainingWindow_ValidCall_ReadsAndMarshalsCorrectly()
    {
        using var dir = new TempDirectory();
        BuildMockDataset(dir);
        using var dataset = await ManifestDataset.LoadAsync(dir.DirectoryPath, rngSeed: 42);

        // pull exactly 1 frame (10ms at 100hz)
        var window = dataset.GetRandomTrainingWindow(TimeSpan.FromMilliseconds(10));

        window.Features.Should().HaveCount(1);
        window.Features[0].Should().HaveCount(2); // 2 features per frame

        // flat data is [1,2,3,4], random pull will either grab [1,2] or [3,4]
        var firstVal = window.Features[0][0];
        firstVal.Should().BeOneOf(1f, 3f);
    }

    [Fact]
    public async Task GetSequentialWindows_ValidCall_SlidesThroughFile()
    {
        using var dir = new TempDirectory();
        BuildMockDataset(dir);
        using var dataset = await ManifestDataset.LoadAsync(dir.DirectoryPath, rngSeed: 42);

        // val file is 20ms long. pulling 10ms window with 10ms stride = 2 windows
        var windows = dataset.GetSequentialWindows(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10)).ToList();

        windows.Should().HaveCount(2);
        
        // window 1 -> [5, 6]
        windows[0].Features[0].Should().BeEquivalentTo(new[] { 5f, 6f });
        windows[0].StartOffset.TotalMilliseconds.Should().Be(0);
        
        // window 2 -> [7, 8]
        windows[1].Features[0].Should().BeEquivalentTo(new[] { 7f, 8f });
        windows[1].StartOffset.TotalMilliseconds.Should().Be(10);
        
        // dog event is at 10-20ms, should only be in second window
        windows[0].GroundTruthEvents.Should().BeEmpty();
        windows[1].GroundTruthEvents.Should().ContainSingle(e => e.Class.ClassName == "Dog");
    }

    [Fact]
    public async Task GetContinuousWindows_StreamsFullChunks()
    {
        using var dir = new TempDirectory();
        BuildMockDataset(dir);
        using var dataset = await ManifestDataset.LoadAsync(dir.DirectoryPath, rngSeed: 42);

        var dataLoader = (IRawContinuousLabeledDataLoader)dataset;
        var chunks = dataLoader.GetContinuousWindows().ToList();

        // should yield one continuous block for the val file
        chunks.Should().HaveCount(1);
        
        // 2 frames, 2 features
        chunks[0].Should().HaveCount(2);
        chunks[0][0].Should().BeEquivalentTo(new[] { 5f, 6f });
        chunks[0][1].Should().BeEquivalentTo(new[] { 7f, 8f });
    }

    [Fact]
    public async Task GetGroundTruthEvents_ShiftsCumulativeTimeAcrossFiles()
    {
        using var dir = new TempDirectory();
        BuildMockDataset(dir);
        
        // inject a second val file to test cumulative shift
        dir.WriteBinary("val2.bin", new[] { 9f, 10f, 11f, 12f });
        var manifestText = File.ReadAllText(Path.Combine(dir.DirectoryPath, DatasetBaker.BakedManifestName));
        var manifest = JsonSerializer.Deserialize(manifestText, DatasetJsonContext.Default.DatasetManifest);
        
        manifest!.CrossValidationStreams.Add(new StreamManifest
        {
            FileName = "val2.bin",
            DurationMs = 20,
            Events = new List<EventManifest> { new() { ClassLabel = "Dog", StartMs = 0, EndMs = 10 } }
        });
        dir.WriteText(DatasetBaker.BakedManifestName, JsonSerializer.Serialize(manifest, DatasetJsonContext.Default.DatasetManifest));

        using var dataset = await ManifestDataset.LoadAsync(dir.DirectoryPath, rngSeed: 42);
        var dataLoader = (IRawContinuousLabeledDataLoader)dataset;

        var events = dataLoader.GetGroundTruthEvents();

        events.Should().HaveCount(2);
        
        // first file event (10ms to 20ms)
        events[0].StartOffset.TotalMilliseconds.Should().Be(10);
        
        // second file event (0ms to 10ms, but shifted by file 1's 20ms duration)
        events[1].StartOffset.TotalMilliseconds.Should().Be(20);
        events[1].EndOffset.TotalMilliseconds.Should().Be(30);
    }
}