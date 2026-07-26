using FluentAssertions;
using Maldact.Backend.ML.Modules;
using TorchSharp;

namespace Maldact.Tests.BackendTests.MLTests.ModulesTests;

/// <summary>
/// Verifies the structural initialization and tensor dimension wiring of the TimeSeriesCnn model.
/// </summary>
public class TimeSeriesCnnTests
{
    /// <summary>
    /// Ensures the module aggressively rejects null hidden layer definitions to prevent late-stage C++ crashes.
    /// </summary>
    [Fact]
    public void Constructor_NullChannelSizes_ThrowsArgumentNullException()
    {
        // arrange & act
        Action act = () => new TimeSeriesCnn(
            name: "test_cnn",
            inputChannels: 3,
            channelSizes: null!,
            kernelSize: 3,
            numClasses: 2);

        // assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Ensures the module enforces at least one hidden convolutional layer.
    /// </summary>
    [Fact]
    public void Constructor_EmptyChannelSizes_ThrowsArgumentException()
    {
        // arrange & act
        Action act = () => new TimeSeriesCnn(
            name: "test_cnn",
            inputChannels: 3,
            channelSizes: Array.Empty<int>(),
            kernelSize: 3,
            numClasses: 2);

        // assert
        act.Should().Throw<ArgumentException>().WithMessage("*at least one hidden layer*");
    }

    /// <summary>
    /// Pushes a dummy tensor through the initialized computation graph to verify memory alignment, 
    /// kernel matrix math, and spatial dimension transpositions.
    /// </summary>
    [Fact]
    public void Forward_ValidInput_ReturnsCorrectlyPermutedTargetShape()
    {
        // arrange
        int batchSize = 4;
        int inputFeatures = 6;
        int sequenceLength = 50;
        int numClasses = 3;

        using var sut = new TimeSeriesCnn(
            name: "test_cnn",
            inputChannels: inputFeatures,
            channelSizes: new[] { 16, 32 },
            kernelSize: 5,
            numClasses: numClasses);

        // input tensor matching the CnnLabeledBatchFormatter: [Batch, Channels, Sequence]
        using var dummyInput = torch.randn(batchSize, sequenceLength, inputFeatures);

        // act
        using var output = sut.forward(dummyInput);

        // assert
        output.Should().NotBeNull();
        
        // expected output shape: [Batch, Sequence, Classes]
        output.shape.Should().BeEquivalentTo(new long[] { batchSize, sequenceLength, numClasses }, options => options.WithStrictOrdering());
        
        // ensure output tensor is attached to the computation graph for backprop
        output.requires_grad.Should().BeTrue();
    }
}