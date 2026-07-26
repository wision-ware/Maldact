using System.Runtime.InteropServices;
using FluentAssertions;
using Maldact.Backend.ML.Training.Data;
using Maldact.Core.Data;
using Maldact.Core.Preprocessing;
using Moq;

namespace Maldact.Tests.BackendTests.MLTests.TrainingTests.DataTests;

/// <summary>
/// Verifies the physical byte-marshaling and offline zero-allocation tensor processing logic of the StreamBaker.
/// </summary>
public class StreamBakerTests
{
    /// <summary>
    /// Lightweight IDisposable wrapper to safely manage physical disk state during testing.
    /// </summary>
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

        public float[] ReadBinary(string fileName)
        {
            var bytes = File.ReadAllBytes(Path.Combine(DirectoryPath, fileName));
            return MemoryMarshal.Cast<byte, float>(bytes.AsSpan()).ToArray();
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true);
        }
    }

    [Fact]
    public void Constructor_NullPipelineFactory_ThrowsArgumentNullException()
    {
        // arrange & act
        Action act = () => new StreamBaker(null!);

        // assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task BakeStreamAsync_InvalidFeatureDimension_ThrowsArgumentOutOfRangeException(int invalidDim)
    {
        // arrange
        var sut = new StreamBaker(() => (new Mock<IDataPreprocessor>().Object, 100f));
        
        // act
        Func<Task> act = async () => await sut.BakeStreamAsync("fake_in.bin", "fake_out.bin", invalidDim);

        // assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task BakeStreamAsync_EmptyInputFile_CreatesEmptyOutputFile()
    {
        // arrange
        using var dir = new TempDirectory();
        string inFile = Path.Combine(dir.DirectoryPath, "empty_in.bin");
        string outFile = Path.Combine(dir.DirectoryPath, "empty_out.bin");
        
        File.WriteAllBytes(inFile, Array.Empty<byte>());

        var sut = new StreamBaker(() => (new Mock<IDataPreprocessor>().Object, 100f));

        // act
        await sut.BakeStreamAsync(inFile, outFile, inputFeatureDim: 2);

        // assert
        File.Exists(outFile).Should().BeTrue();
        new FileInfo(outFile).Length.Should().Be(0);
    }

    [Fact]
    public async Task BakeStreamAsync_ValidData_ProcessesAndFlattensCorrectly()
    {
        // arrange
        using var dir = new TempDirectory();
        
        // 3 frames, 2 features per frame
        float[] rawData = { 1f, 2f, 3f, 4f, 5f, 6f };
        dir.WriteBinary("raw.bin", rawData);
        
        string inPath = Path.Combine(dir.DirectoryPath, "raw.bin");
        string outPath = Path.Combine(dir.DirectoryPath, "baked.bin");

        int framesProcessed = 0;

        // mock pipeline adds 10 to every float via zero-allocation span processing
        var mockPipeline = new Mock<IDataPreprocessor>();
        mockPipeline.Setup(p => p.Process(It.IsAny<PipelineChunk>()))
            .Callback<PipelineChunk>(chunk =>
            {
                ReadOnlySpan<float> input = chunk.CurrentData;
                
                // capture structural count before chunk is naturally disposed
                framesProcessed = input.Length / 2; 
                
                Span<float> output = chunk.AdvancePreprocessingStep(input.Length);
                for (int i = 0; i < input.Length; i++)
                {
                    output[i] = input[i] + 10f;
                }
            });

        StreamBaker.PipelineFactory factory = () => (mockPipeline.Object, 100f);
        var sut = new StreamBaker(factory);

        // act
        await sut.BakeStreamAsync(inPath, outPath, inputFeatureDim: 2);

        // assert
        var bakedData = dir.ReadBinary("baked.bin");
        
        // expected: 3 frames, 2 features -> completely flattened contiguous array
        bakedData.Should().HaveCount(6);
        bakedData.Should().BeEquivalentTo(new float[]
        {
            11f, 12f, 
            13f, 14f, 
            15f, 16f
        }, options => options.WithStrictOrdering());
        
        // verify pipeline state logic
        framesProcessed.Should().Be(3, "the pipeline must be injected with exactly 3 frames of data.");
        mockPipeline.Verify(p => p.Process(It.IsAny<PipelineChunk>()), Times.Once);
    }
}