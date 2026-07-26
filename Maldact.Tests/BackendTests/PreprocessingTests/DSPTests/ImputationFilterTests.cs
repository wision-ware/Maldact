using FluentAssertions;
using Maldact.Backend.Preprocessing.DSPs;
using Maldact.Core.Data;

namespace Maldact.Tests.BackendTests.PreprocessingTests.DSPTests;

public class ImputationFilterTests
{
    [Fact]
    public void Process_EmptyChunk_ReturnsEmptyArray()
    {
        var sut = new ImputationFilter(ImputationFilter.ImputationMethod.ZeroFill, dimension: 2);
        using var chunk = new PipelineChunk(Array.Empty<float[]>(), 2);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void Process_ZeroFill_ReplacesAllInvalidValuesWithZero()
    {
        var sut = new ImputationFilter(ImputationFilter.ImputationMethod.ZeroFill, dimension: 1);
        var input = new[]
        {
            new[] { 10f },
            new[] { float.NaN },
            new[] { 20f },
            new[] { float.PositiveInfinity },
            new[] { float.NegativeInfinity }
        };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 10f, 0f, 20f, 0f, 0f });
    }

    [Fact]
    public void Process_ForwardFill_ReplacesInvalidValuesWithLastKnownValue()
    {
        var sut = new ImputationFilter(ImputationFilter.ImputationMethod.ForwardFill, dimension: 1);
        var input = new[]
        {
            new[] { 10f },
            new[] { float.NaN },
            new[] { float.NaN }, 
            new[] { 20f },
            new[] { float.PositiveInfinity }
        };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 10f, 10f, 10f, 20f, 20f });
    }

    [Fact]
    public void Process_ForwardFill_FirstFrameInvalid_DefaultsToZero()
    {
        var sut = new ImputationFilter(ImputationFilter.ImputationMethod.ForwardFill, dimension: 1);
        var input = new[] { new[] { float.NaN }, new[] { 15f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 0f, 15f });
    }

    [Fact]
    public void Process_ForwardFill_MaintainsStateAcrossStreamingChunks()
    {
        var sut = new ImputationFilter(ImputationFilter.ImputationMethod.ForwardFill, dimension: 1);
        
        var input1 = new[] { new[] { 42f } };
        var input2 = new[] { new[] { float.NaN } };

        using var chunk1 = new PipelineChunk(input1, 1);
        using var chunk2 = new PipelineChunk(input2, 1);

        sut.Process(chunk1);
        sut.Process(chunk2);

        chunk1.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 42f });
        chunk2.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 42f });
    }

    [Fact]
    public void Process_MultipleDimensions_ImputesIndependently()
    {
        var sut = new ImputationFilter(ImputationFilter.ImputationMethod.ForwardFill, dimension: 2);
        var input = new[]
        {
            new[] { 10f, 100f },
            new[] { float.NaN, 200f },
            new[] { 30f, float.PositiveInfinity }
        };
        using var chunk = new PipelineChunk(input, 2);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 10f, 100f, 10f, 200f, 30f, 200f });
    }
}