using FluentAssertions;
using Maldact.Backend.Preprocessing.DSPs;
using Maldact.Core.Data;

namespace Maldact.Tests.BackendTests.PreprocessingTests.DSPTests;

public class RescalerTests
{
    [Fact]
    public void Constructor_NullFunction_ThrowsArgumentNullException()
    {
        Action act = () => new Rescaler(dimension: 1, vectorizedRescalingFunction: null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Process_EmptyChunk_ReturnsEmptyArray()
    {
        var sut = new Rescaler(dimension: 1, (Span<float> arr) => { });
        using var chunk = new PipelineChunk(Array.Empty<float[]>(), 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void Process_ValidChunk_AppliesRescalingFunction()
    {
        var sut = new Rescaler(dimension: 2, (Span<float> arr) => 
        {
            for (int i = 0; i < arr.Length; i++) arr[i] *= 2f;
        });
        
        var input = new[] { new[] { 1f, 10f }, new[] { 2f, 20f } };
        using var chunk = new PipelineChunk(input, 2);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new[] { 2f, 20f, 4f, 40f });
    }

    [Fact]
    public void Process_ValidChunk_DoesNotMutateOriginalData()
    {
        var sut = new Rescaler(dimension: 1, (Span<float> arr) => arr[0] = 999f);
        var input = new[] { new[] { 42f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray()[0].Should().Be(999f);
        
        // The original array passed to the PipelineChunk constructor must remain intact
        input[0][0].Should().Be(42f); 
    }
}