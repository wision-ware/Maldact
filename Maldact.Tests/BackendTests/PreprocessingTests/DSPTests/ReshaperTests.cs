using FluentAssertions;
using Maldact.Backend.Preprocessing.DSPs;
using Maldact.Core.Data;

namespace Maldact.Tests.BackendTests.PreprocessingTests.DSPTests;

public class ReshaperTests
{
    [Fact]
    public void Process_EmptyChunk_ReturnsEmptyArray()
    {
        var sut = new Reshaper(inputDimension: 2, targetDimension: 3, Reshaper.ReshapeMethod.TruncateOrZeroFill);
        using var chunk = new PipelineChunk(Array.Empty<float[]>(), 2);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void Process_DimensionsPerfectlyMatch_BypassesLogicAndReturnsOriginal()
    {
        var sut = new Reshaper(inputDimension: 2, targetDimension: 2, Reshaper.ReshapeMethod.Strict);
        var input = new[] { new[] { 1f, 2f } };
        using var chunk = new PipelineChunk(input, 2);

        sut.Process(chunk);

        // Bypasses logic entirely, state remains Initial
        chunk.State.Should().Be(ChunkState.Initial);
        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 1f, 2f });
    }

    [Fact]
    public void Process_StrictMethodWithMismatch_ThrowsInvalidOperationException()
    {
        var sut = new Reshaper(inputDimension: 2, targetDimension: 3, Reshaper.ReshapeMethod.Strict);
        var input = new[] { new[] { 1f, 2f } };
        using var chunk = new PipelineChunk(input, 2);

        Action act = () => sut.Process(chunk);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Process_TruncateOrZeroFill_Upsizing_PadsWithZeroes()
    {
        var sut = new Reshaper(inputDimension: 2, targetDimension: 4, Reshaper.ReshapeMethod.TruncateOrZeroFill);
        var input = new[] { new[] { 10f, 20f } };
        using var chunk = new PipelineChunk(input, 2);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 10f, 20f, 0f, 0f });
    }

    [Fact]
    public void Process_TruncateOrZeroFill_Downsizing_TruncatesExcess()
    {
        var sut = new Reshaper(inputDimension: 4, targetDimension: 2, Reshaper.ReshapeMethod.TruncateOrZeroFill);
        var input = new[] { new[] { 10f, 20f, 30f, 40f } };
        using var chunk = new PipelineChunk(input, 4);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 10f, 20f });
    }

    [Fact]
    public void Process_Interpolation_Upsampling_StretchesAndInterpolates()
    {
        var sut = new Reshaper(inputDimension: 2, targetDimension: 3, Reshaper.ReshapeMethod.Interpolation);
        var input = new[] { new[] { 10f, 30f } };
        using var chunk = new PipelineChunk(input, 2);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 10f, 20f, 30f });
    }

    [Fact]
    public void Process_Interpolation_Downsampling_SquashesAndInterpolates()
    {
        var sut = new Reshaper(inputDimension: 3, targetDimension: 2, Reshaper.ReshapeMethod.Interpolation);
        var input = new[] { new[] { 10f, 20f, 30f } };
        using var chunk = new PipelineChunk(input, 3);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 10f, 30f });
    }

    [Fact]
    public void Process_Interpolation_ToSingleDimension_TakesFirstElement()
    {
        var sut = new Reshaper(inputDimension: 3, targetDimension: 1, Reshaper.ReshapeMethod.Interpolation);
        var input = new[] { new[] { 42f, 99f, 100f } };
        using var chunk = new PipelineChunk(input, 3);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 42f });
    }

    [Fact]
    public void Process_Interpolation_FromSingleDimension_BroadcastsToAllElements()
    {
        var sut = new Reshaper(inputDimension: 1, targetDimension: 3, Reshaper.ReshapeMethod.Interpolation);
        var input = new[] { new[] { 42f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 42f, 42f, 42f });
    }
}