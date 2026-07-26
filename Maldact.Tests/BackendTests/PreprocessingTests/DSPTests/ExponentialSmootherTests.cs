using FluentAssertions;
using Maldact.Backend.Preprocessing.DSPs;
using Maldact.Core.Data;

namespace Maldact.Tests.BackendTests.PreprocessingTests.DSPTests;

public class ExponentialSmootherTests
{
    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    public void Constructor_InvalidGamma_ThrowsArgumentOutOfRangeException(float invalidGamma)
    {
        Action act = () => new ExponentialSmoother(dimension: 1, windowSize: 3, gamma: invalidGamma);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Process_EmptyChunk_ReturnsEmptyArray()
    {
        var sut = new ExponentialSmoother(dimension: 2, windowSize: 3);
        using var chunk = new PipelineChunk(Array.Empty<float[]>(), 2);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void Process_NullGamma_CalculatesDefaultGammaCorrectly()
    {
        var sut = new ExponentialSmoother(dimension: 1, windowSize: 9, gamma: null);
        var input = new[] { new[] { 100f }, new[] { 200f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        var result = chunk.CurrentData.ToArray();
        // math: (200 * 0.2) + (100 * 0.8) = 120
        result[1].Should().BeApproximately(120f, 0.0001f);
    }

    [Fact]
    public void Process_ContinuousChunk_AppliesExponentialSmoothing()
    {
        var sut = new ExponentialSmoother(dimension: 1, windowSize: 3, gamma: 0.5f);
        var input = new[] { new[] { 10f }, new[] { 20f }, new[] { 30f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 10f, 15f, 22.5f });
    }

    [Fact]
    public void Process_MultipleChunks_MaintainsStateAcrossBoundaries()
    {
        var sut = new ExponentialSmoother(dimension: 1, windowSize: 3, gamma: 0.5f);
        
        var input1 = new[] { new[] { 10f } };
        var input2 = new[] { new[] { 20f }, new[] { 30f } };

        using var chunk1 = new PipelineChunk(input1, 1);
        using var chunk2 = new PipelineChunk(input2, 1);

        sut.Process(chunk1);
        sut.Process(chunk2);

        chunk1.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 10f });
        chunk2.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 15f, 22.5f });
    }

    [Fact]
    public void Process_MultidimensionalData_SmoothsIndependentFeatures()
    {
        var sut = new ExponentialSmoother(dimension: 2, windowSize: 3, gamma: 0.5f);
        var input = new[] { new[] { 10f, 100f }, new[] { 20f, 200f } };
        using var chunk = new PipelineChunk(input, 2);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 10f, 100f, 15f, 150f });
    }
}