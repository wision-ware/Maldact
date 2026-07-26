using FluentAssertions;
using Maldact.Backend.Preprocessing.DSPs;
using Maldact.Core.Data;

namespace Maldact.Tests.BackendTests.PreprocessingTests.DSPTests;

public class MinMaxNormalizerTests
{
    [Fact]
    public void Constructor_InvalidGlobalMinsLength_ThrowsArgumentException()
    {
        Action act = () => new MinMaxNormalizer(dimension: 2, globalMins: new[] { 0f });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_InvalidGlobalMaxesLength_ThrowsArgumentException()
    {
        Action act = () => new MinMaxNormalizer(dimension: 2, globalMins: null, globalMaxes: new[] { 1f, 2f, 3f });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Process_EmptyChunk_ReturnsEmptyArray()
    {
        var sut = new MinMaxNormalizer(dimension: 1);
        using var chunk = new PipelineChunk(Array.Empty<float[]>(), 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void Process_ZeroVariance_SafelyReturnsZeroes()
    {
        var sut = new MinMaxNormalizer(dimension: 1);
        var input = new[] { new[] { 42f }, new[] { 42f }, new[] { 42f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 0f, 0f, 0f });
    }

    [Fact]
    public void Process_LocalBounds_CalculatesAndScalesCorrectly()
    {
        var sut = new MinMaxNormalizer(dimension: 1);
        var input = new[] { new[] { 10f }, new[] { 15f }, new[] { 20f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 0f, 0.5f, 1f });
    }

    [Fact]
    public void Process_GlobalBounds_UsesProvidedExtremes()
    {
        var sut = new MinMaxNormalizer(dimension: 1, globalMins: new[] { 0f }, globalMaxes: new[] { 100f });
        var input = new[] { new[] { 25f }, new[] { 50f }, new[] { 75f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 0.25f, 0.50f, 0.75f });
    }

    [Fact]
    public void Process_GlobalBounds_AllowsValuesOutsideZeroToOne()
    {
        var sut = new MinMaxNormalizer(dimension: 1, globalMins: new[] { 10f }, globalMaxes: new[] { 20f });
        var input = new[] { new[] { 5f }, new[] { 25f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { -0.5f, 1.5f });
    }

    [Fact]
    public void Process_MultipleDimensions_ScalesIndependently()
    {
        var sut = new MinMaxNormalizer(dimension: 2);
        var input = new[] { new[] { 0f, 100f }, new[] { 5f, 150f }, new[] { 10f, 200f } };
        using var chunk = new PipelineChunk(input, 2);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 0f, 0f, 0.5f, 0.5f, 1f, 1f });
    }
}