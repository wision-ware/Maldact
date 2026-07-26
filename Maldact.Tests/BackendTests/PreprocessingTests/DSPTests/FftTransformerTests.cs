using FluentAssertions;
using Maldact.Backend.Preprocessing.DSPs;
using Maldact.Core.Data;

namespace Maldact.Tests.BackendTests.PreprocessingTests.DSPTests;

public class FftTransformerTests
{
    [Theory]
    [InlineData(2, 5, false, 10)] 
    [InlineData(2, 5, true, 20)]  
    public void Constructor_CalculatesOutputDimensionCorrectly(
        int inputDim, int freqsToKeep, bool includePhase, int expectedOutputDim)
    {
        var sut = new FftTransformer(inputDim, windowSize: 10, freqsToKeep, includePhase);
        sut.OutputDimension.Should().Be(expectedOutputDim);
    }

    [Fact]
    public void Process_EmptyChunk_ReturnsEmptyArray()
    {
        var sut = new FftTransformer(1, 4, 2, false);
        using var chunk = new PipelineChunk(Array.Empty<float[]>(), 1);

        sut.Process(chunk);

        chunk.CurrentData.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void Process_WindowNotFull_YieldsZeroes()
    {
        var sut = new FftTransformer(inputDimension: 1, windowSize: 4, frequenciesToKeep: 2, includePhase: false);
        var input = new[] { new[] { 10f }, new[] { 10f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        var result = chunk.CurrentData.ToArray();
        result.Length.Should().Be(4); // 2 frames * 2 output dims
        result.Should().OnlyContain(val => val == 0f);
    }

    [Fact]
    public void Process_WindowFull_ComputesFftCorrectly()
    {
        var sut = new FftTransformer(inputDimension: 1, windowSize: 4, frequenciesToKeep: 2, includePhase: false);
        var input = new[] { new[] { 10f }, new[] { 10f }, new[] { 10f }, new[] { 10f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        var result = chunk.CurrentData.ToArray();
        
        // first 3 frames (indices 0 to 5) must be zero
        for (int i = 0; i < 6; i++) result[i].Should().Be(0f);

        // 4th frame (indices 6 and 7) triggers FFT
        result[6].Should().BeApproximately(40f, 0.0001f);
        result[7].Should().BeApproximately(0f, 0.0001f);
    }

    [Fact]
    public void Process_StreamingChunks_MaintainsCircularBufferProperly()
    {
        var sut = new FftTransformer(inputDimension: 1, windowSize: 3, frequenciesToKeep: 2, includePhase: false);

        var input1 = new[] { new[] { 5f }, new[] { 5f } };
        var input2 = new[] { new[] { 5f }, new[] { 5f } };

        using var chunk1 = new PipelineChunk(input1, 1);
        using var chunk2 = new PipelineChunk(input2, 1);

        sut.Process(chunk1);
        sut.Process(chunk2);

        chunk1.CurrentData.ToArray().Should().OnlyContain(v => v == 0f);

        var result2 = chunk2.CurrentData.ToArray();
        // chunk 2 frame 0: window reaches 3 frames, computes fft of [5,5,5] -> dc is 15
        result2[0].Should().BeApproximately(15f, 0.0001f);
        // chunk 2 frame 1: window slides to [5,5,5], dc is still 15
        result2[2].Should().BeApproximately(15f, 0.0001f);
    }

    [Fact]
    public void Process_WithPhase_AppendsPhaseDataProperly()
    {
        var sut = new FftTransformer(inputDimension: 1, windowSize: 4, frequenciesToKeep: 2, includePhase: true);
        var input = new[] { new[] { 10f }, new[] { -10f }, new[] { 10f }, new[] { -10f } };
        using var chunk = new PipelineChunk(input, 1);

        sut.Process(chunk);

        var result = chunk.CurrentData.ToArray();
        result.Length.Should().Be(16); // 4 frames * 4 output dims

        // Final frame starts at index 12
        result[12].Should().BeApproximately(0f, 0.0001f); 
        result[14].Should().BeInRange(-MathF.PI, MathF.PI); 
        result[15].Should().BeInRange(-MathF.PI, MathF.PI);
    }

    [Fact]
    public void Process_MultipleDimensions_TransformsIndependently()
    {
        var sut = new FftTransformer(inputDimension: 2, windowSize: 2, frequenciesToKeep: 1, includePhase: false);
        var input = new[] { new[] { 10f, 20f }, new[] { 10f, 20f } };
        using var chunk = new PipelineChunk(input, 2);

        sut.Process(chunk);

        var result = chunk.CurrentData.ToArray();
        // Target shape flat length: 2 frames * 2 output dims = 4
        // Final frame starts at index 2
        result[2].Should().BeApproximately(20f, 0.0001f); 
        result[3].Should().BeApproximately(40f, 0.0001f); 
    }
}