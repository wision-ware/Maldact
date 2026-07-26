using FluentAssertions;
using Maldact.Backend.Preprocessing.DSPs;
using Maldact.Core.Data;

namespace Maldact.Tests.BackendTests.PreprocessingTests.DSPTests;

public class ResamplerTests
{
    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, -10)]
    public void Constructor_InvalidFrequencies_ThrowsArgumentOutOfRangeException(double sourceHz, double targetHz)
    {
        Action act = () => new Resampler(1, sourceHz, targetHz, Resampler.AggregationFunction.Mean);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Process_EmptyChunk_ReturnsEmptyArray()
    {
        var sut = new Resampler(1, 100, 50, Resampler.AggregationFunction.Mean);
        using var chunk = new PipelineChunk(Array.Empty<float[]>(), 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void Process_MatchingFrequencies_ReturnsOriginalChunkBypassingLogic()
    {
        var sut = new Resampler(1, 100.0, 100.00001, Resampler.AggregationFunction.Mean);
        var input = new[] { new[] { 1f }, new[] { 2f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        // State should remain strictly Initial, and arrays perfectly match
        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 1f, 2f });
    }

    [Fact]
    public void Process_DownsampleMean_AggregatesBucketsCorrectly()
    {
        var sut = new Resampler(1, 100, 50, Resampler.AggregationFunction.Mean);
        var input = new[] { new[] { 10f }, new[] { 20f }, new[] { 30f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 15f });
    }

    [Theory]
    [InlineData(Resampler.AggregationFunction.Max, 20f)]
    [InlineData(Resampler.AggregationFunction.Min, 10f)]
    [InlineData(Resampler.AggregationFunction.First, 10f)]
    [InlineData(Resampler.AggregationFunction.Last, 20f)]
    public void Process_Downsample_AppliesCorrectAggregationFunction(
        Resampler.AggregationFunction function, float expectedResult)
    {
        var sut = new Resampler(1, 100, 50, function);
        var input = new[] { new[] { 10f }, new[] { 20f }, new[] { 30f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray()[0].Should().Be(expectedResult);
    }

    [Fact]
    public void Process_Upsample_InterpolatesFramesCorrectly()
    {
        var sut = new Resampler(1, 50, 100, Resampler.AggregationFunction.Mean);
        var input = new[] { new[] { 10f }, new[] { 20f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 10f, 15f, 20f });
    }

    [Fact]
    public void Process_Downsample_MaintainsStateAcrossStreamingChunks()
    {
        var sut = new Resampler(1, 100, 50, Resampler.AggregationFunction.Mean);
        
        var input1 = new[] { new[] { 10f }, new[] { 20f } };
        var input2 = new[] { new[] { 30f } };

        using var chunk1 = new PipelineChunk(input1, 1);
        using var chunk2 = new PipelineChunk(input2, 1);

        sut.Process(chunk1);
        sut.Process(chunk2);

        chunk1.CurrentData.ToArray().Should().BeEmpty();
        chunk2.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 15f });
    }

    [Fact]
    public void Process_Upsample_MaintainsStateAcrossStreamingChunks()
    {
        var sut = new Resampler(1, 50, 100, Resampler.AggregationFunction.Mean);
        
        var input1 = new[] { new[] { 10f } }; 
        var input2 = new[] { new[] { 20f } }; 

        using var chunk1 = new PipelineChunk(input1, 1);
        using var chunk2 = new PipelineChunk(input2, 1);

        sut.Process(chunk1);
        sut.Process(chunk2);

        chunk1.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 10f });
        chunk2.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 15f, 20f });
    }

    [Fact]
    public void Process_MultipleDimensions_CalculatesIndependently()
    {
        var sut = new Resampler(2, 50, 100, Resampler.AggregationFunction.Mean);
        var input = new[] { new[] { 10f, 100f }, new[] { 20f, 300f } };
        using var chunk = new PipelineChunk(input, 2);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 10f, 100f, 15f, 200f, 20f, 300f });
    }
}