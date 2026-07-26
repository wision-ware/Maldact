using FluentAssertions;
using Maldact.Backend.Preprocessing.DSPs;
using Maldact.Backend.Preprocessing.Pipelines;
using Maldact.Core.Data;
using Maldact.Core.Preprocessing;

namespace Maldact.Tests.BackendTests.PreprocessingTests.PipelinesTests;

public class ZScoreNormalizerTests
{
    [Fact]
    public void Constructor_InvalidGlobalMeansLength_ThrowsArgumentException()
    {
        Action act = () => new ZScoreNormalizer(dimension: 2, globalMeans: new[] { 0f });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_InvalidGlobalStdDevsLength_ThrowsArgumentException()
    {
        Action act = () => new ZScoreNormalizer(dimension: 2, globalMeans: null, globalStdDevs: new[] { 1f, 2f, 3f });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Process_EmptyChunk_ReturnsEmptyArray()
    {
        var sut = new ZScoreNormalizer(dimension: 1);
        using var chunk = new PipelineChunk(Array.Empty<float[]>(), 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void Process_ZeroVariance_SafelyReturnsZeroes()
    {
        var sut = new ZScoreNormalizer(dimension: 1);
        var input = new[] { new[] { 42f }, new[] { 42f }, new[] { 42f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 0f, 0f, 0f });
    }

    [Fact]
    public void Process_LocalStats_CalculatesAndScalesCorrectly()
    {
        var sut = new ZScoreNormalizer(dimension: 1);
        var input = new[] { new[] { 0f }, new[] { 10f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { -1f, 1f });
    }

    [Fact]
    public void Process_GlobalStats_UsesProvidedValues()
    {
        var sut = new ZScoreNormalizer(dimension: 1, globalMeans: new[] { 10f }, globalStdDevs: new[] { 2f });
        var input = new[] { new[] { 8f }, new[] { 10f }, new[] { 14f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { -1f, 0f, 2f });
    }

    [Fact]
    public void Process_MultipleDimensions_ScalesIndependently()
    {
        var sut = new ZScoreNormalizer(dimension: 2);
        var input = new[] { new[] { 0f, 10f }, new[] { 10f, 30f } };
        using var chunk = new PipelineChunk(input, 2);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { -1f, -1f, 1f, 1f });
    }
}