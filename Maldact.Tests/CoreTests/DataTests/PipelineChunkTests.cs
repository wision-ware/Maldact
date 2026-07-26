using System.Runtime.InteropServices;
using FluentAssertions;
using Maldact.Core.Data;
using Maldact.Core.Results;

namespace Maldact.Tests.CoreTests.DataTests;

public class PipelineChunkTests
{
    [Fact]
    public void Constructor_ValidDimensions_InitializesCorrectly()
    {
        // arrange & act
        using var chunk = PipelineChunk.Rent(frames: 10, featureDim: 3);

        // assert
        chunk.State.Should().Be(ChunkState.Initial);
        chunk.CurrentData.Length.Should().Be(30);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 5)]
    public void Constructor_InvalidDimensions_ThrowsArgumentOutOfRangeException(int frames, int dim)
    {
        // arrange & act
        Action act = () => PipelineChunk.Rent(frames, dim);

        // assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void JaggedConstructor_FlattensDataCorrectly()
    {
        // arrange
        float[][] jagged = 
        [
            [1f, 2f],
            [3f, 4f],
            [5f, 6f]
        ];

        // act
        using var chunk = new PipelineChunk(jagged, featureDim: 2);

        // assert
        chunk.State.Should().Be(ChunkState.Initial);
        chunk.CurrentData.Length.Should().Be(6);
        chunk.CurrentData.ToArray().Should().BeEquivalentTo(new float[] { 1f, 2f, 3f, 4f, 5f, 6f }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void DecodeRawNetworkBytes_ValidBytes_CopiesToFloatBuffer()
    {
        // arrange
        using var chunk = PipelineChunk.Rent(frames: 2, featureDim: 2); // expects 4 floats
        float[] expectedFloats = { 10.5f, 20.5f, 30.5f, 40.5f };
        byte[] rawBytes = MemoryMarshal.AsBytes(expectedFloats.AsSpan()).ToArray();

        // act
        chunk.DecodeRawNetworkBytes(rawBytes, 0, rawBytes.Length);

        // assert
        chunk.CurrentData.ToArray().Should().BeEquivalentTo(expectedFloats, options => options.WithStrictOrdering());
    }

    [Fact]
    public void DecodeRawNetworkBytes_NotInInitialState_ThrowsInvalidOperationException()
    {
        // arrange
        using var chunk = PipelineChunk.Rent(1, 1);
        chunk.AdvancePreprocessingStep(1); // shifts to Preprocessing state
        
        // act
        Action act = () => chunk.DecodeRawNetworkBytes(Array.Empty<byte>(), 0, 0);

        // assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*uninitialized initial chunk*");
    }

    [Fact]
    public void AdvancePreprocessingStep_TransitionsStateAndResizesBuffer()
    {
        // arrange
        using var chunk = PipelineChunk.Rent(frames: 2, featureDim: 2); // initially 4 floats
        
        // act
        var newSpan = chunk.AdvancePreprocessingStep(10); // expand buffer to 10

        // assert
        chunk.State.Should().Be(ChunkState.Preprocessing);
        newSpan.Length.Should().Be(10);
        chunk.CurrentData.Length.Should().Be(10);
    }

    [Fact]
    public void TransitionToInference_FromPreprocessing_Succeeds()
    {
        // arrange
        using var chunk = PipelineChunk.Rent(10, 2);
        chunk.AdvancePreprocessingStep(20);
        
        // act
        var infSpan = chunk.TransitionToInference(5);

        // assert
        chunk.State.Should().Be(ChunkState.Inferred);
        infSpan.Length.Should().Be(5);
    }

    [Fact]
    public void TransitionToInference_Twice_ThrowsInvalidOperationException()
    {
        // arrange
        using var chunk = PipelineChunk.Rent(1, 1);
        chunk.TransitionToInference(1);
        
        // act
        Action act = () => chunk.TransitionToInference(1);

        // assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FullLifecycle_YieldsResultsAndReleasesMemory()
    {
        // arrange
        using var chunk = PipelineChunk.Rent(10, 3);
        var dummyResults = Array.Empty<ResultEntry>(); // Replace with dummy data if ResultEntry requires it

        // act
        chunk.AdvancePreprocessingStep(15);
        chunk.TransitionToInference(5);
        chunk.TransitionToConsolidated(dummyResults);
        var final = chunk.YieldFinal();

        // assert
        chunk.State.Should().Be(ChunkState.Consolidated);
        final.Should().BeSameAs(dummyResults);
        
        // Because the array was returned to the pool and nulled out, accessing CurrentData 
        // will inherently throw an ArgumentOutOfRangeException (null.AsSpan(0, length)). 
        // This test documents that behavior so future devs don't access it post-consolidation.
        Action actCurrent = () => { var _ = chunk.CurrentData; };
        actCurrent.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // arrange
        var chunk = PipelineChunk.Rent(10, 2);
        
        // act
        chunk.Dispose();
        Action act = () => chunk.Dispose(); // second call

        // assert
        act.Should().NotThrow();
        chunk.State.Should().Be(ChunkState.Disposed);
    }

    [Fact]
    public void CurrentData_AfterDispose_ThrowsObjectDisposedException()
    {
        // arrange
        var chunk = PipelineChunk.Rent(1, 1);
        chunk.Dispose();

        // act
        Action act = () => { var _ = chunk.CurrentData; };

        // assert
        act.Should().Throw<ObjectDisposedException>();
    }
}