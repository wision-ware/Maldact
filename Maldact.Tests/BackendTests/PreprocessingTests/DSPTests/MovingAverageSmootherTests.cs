using FluentAssertions;
using Maldact.Backend.Preprocessing.DSPs;
using Maldact.Core.Data;

namespace Maldact.Tests.BackendTests.PreprocessingTests.DSPTests;

public class MovingAverageSmootherTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidWindowSize_ThrowsArgumentOutOfRangeException(int invalidWindowSize)
    {
        Action act = () => new MovingAverageSmoother(dimension: 1, windowSize: invalidWindowSize);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Process_EmptyChunk_ReturnsEmptyArray()
    {
        var sut = new MovingAverageSmoother(dimension: 2, windowSize: 5);
        using var chunk = new PipelineChunk(Array.Empty<float[]>(), 2);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void Process_PartialWindow_DivisorGrowsDynamically()
    {
        var sut = new MovingAverageSmoother(dimension: 1, windowSize: 4);
        var input = new[] { new[] { 10f }, new[] { 20f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 10f, 15f });
    }

    [Fact]
    public void Process_FullWindow_DropsOldestValuesFromRunningSum()
    {
        var sut = new MovingAverageSmoother(dimension: 1, windowSize: 3);
        var input = new[]
        {
            new[] { 10f }, 
            new[] { 20f }, 
            new[] { 30f }, 
            new[] { 40f }, 
            new[] { 50f }  
        };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 10f, 15f, 20f, 30f, 40f });
    }

    [Fact]
    public void Process_StreamingChunks_MaintainsRunningSumAndBufferPointers()
    {
        var sut = new MovingAverageSmoother(dimension: 1, windowSize: 3);
        
        var input1 = new[] { new[] { 10f }, new[] { 20f } };
        var input2 = new[] { new[] { 30f }, new[] { 40f }, new[] { 50f } };

        using var chunk1 = new PipelineChunk(input1, 1);
        using var chunk2 = new PipelineChunk(input2, 1);

        sut.Process(chunk1);
        sut.Process(chunk2);

        chunk1.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 10f, 15f });
        chunk2.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 20f, 30f, 40f });
    }

    [Fact]
    public void Process_MultipleDimensions_CalculatesIndependently()
    {
        var sut = new MovingAverageSmoother(dimension: 2, windowSize: 2);
        var input = new[]
        {
            new[] { 10f, 100f },
            new[] { 20f, 300f },
            new[] { 30f, 500f }
        };
        using var chunk = new PipelineChunk(input, 2);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 10f, 100f, 15f, 200f, 25f, 400f });
    }
}