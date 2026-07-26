using FluentAssertions;
using Maldact.Backend.ML.Modules;
using TorchSharp;

namespace Maldact.Tests.BackendTests.MLTests.ModulesTests;

/// <summary>
/// Verifies the structural initialization and tensor dimension wiring of the TimeSeriesGru model.
/// </summary>
public class TimeSeriesGruTests
{
    /// <summary>
    /// Ensures the module aggressively rejects invalid dimensions to prevent c++ math crashes.
    /// </summary>
    [Theory]
    [InlineData(0, 32, 2)]
    [InlineData(10, -1, 2)]
    [InlineData(10, 32, 0)]
    public void Constructor_InvalidDimensions_ThrowsArgumentOutOfRangeException(
        int inputDim, int hiddenSize, int numClasses)
    {
        // arrange & act
        Action act = () => new TimeSeriesGru(
            name: "test_gru",
            inputDimension: inputDim,
            hiddenSize: hiddenSize,
            numLayers: 1,
            numClasses: numClasses,
            dropout: 0.0);

        // assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Pushes a dummy tensor through the initialized computation graph to verify memory alignment, 
    /// recurrent unrolling, and linear projections.
    /// </summary>
    [Fact]
    public void Forward_ValidInput_ReturnsCorrectlyShapedTargetTensor()
    {
        // arrange
        int batchSize = 4;
        int inputFeatures = 6;
        int sequenceLength = 50;
        int hiddenSize = 32;
        int numClasses = 3;

        using var sut = new TimeSeriesGru(
            name: "test_gru",
            inputDimension: inputFeatures,
            hiddenSize: hiddenSize,
            numLayers: 2,
            numClasses: numClasses,
            dropout: 0.1);

        // input tensor matching the Formatter: [Batch, Sequence, Features]
        using var dummyInput = torch.randn(batchSize, sequenceLength, inputFeatures);

        // act
        using var output = sut.forward(dummyInput);

        // assert
        output.Should().NotBeNull();
        
        // expected output shape: [Batch, Sequence, Classes]
        output.shape.Should().BeEquivalentTo(
            new long[] { batchSize, sequenceLength, numClasses }, 
            options => options.WithStrictOrdering());
        
        // ensure output tensor is attached to the computation graph for backprop
        output.requires_grad.Should().BeTrue();
    }
}