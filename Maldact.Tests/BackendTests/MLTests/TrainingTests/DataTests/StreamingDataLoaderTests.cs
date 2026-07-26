using FluentAssertions;
using Maldact.Backend.ML.Training.Data;
using Maldact.Core.Data;
using Maldact.Core.ML.Training;
using Moq;

namespace Maldact.Tests.BackendTests.MLTests.TrainingTests.DataTests;

public class StreamingDataLoaderTests
{
    private readonly Mock<IRawLabeledTrainingDataLoader> _mockDataLoader = new();
    private readonly Mock<ILabeledBatchFormatter> _mockFormatter = new();
    
    private readonly TimeSpan _windowDuration = TimeSpan.FromSeconds(1);
    private readonly TimeSpan _stride = TimeSpan.FromMilliseconds(500);

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, -1)]
    public void Constructor_InvalidBatchOrEpochCounts_ThrowsArgumentOutOfRangeException(int batchSize, int epochs)
    {
        // arrange & act
        Action act = () => new StreamingDataLoader(
            _mockDataLoader.Object, 
            _mockFormatter.Object, 
            batchSize, 
            epochs, 
            _stride, 
            _windowDuration);

        // assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GenerateBatches_ValidConfig_YieldsCorrectNumberOfBatchesAndWindows()
    {
        // arrange
        int batchSize = 3;
        int batchesPerEpoch = 5;
        
        var dummyWindow = new RawTrainingWindow(Array.Empty<float[]>(), Array.Empty<AbsoluteEvent>(), TimeSpan.Zero, TimeSpan.Zero);
        _mockDataLoader.Setup(x => x.GetRandomTrainingWindow(It.IsAny<TimeSpan>())).Returns(dummyWindow);
        _mockFormatter.Setup(x => x.Format(It.IsAny<IReadOnlyList<RawTrainingWindow>>())).Returns(FormattedLabeledBatch.Empty);

        var sut = new StreamingDataLoader(
            _mockDataLoader.Object, 
            _mockFormatter.Object, 
            batchSize, 
            batchesPerEpoch, 
            _stride, 
            _windowDuration);

        // act
        var batches = sut.GenerateBatches().ToList();

        // assert
        batches.Should().HaveCount(5); // exactly 5 batches per epoch
        
        // verify data loader was hit exactly (batchSize * batchesPerEpoch) times
        _mockDataLoader.Verify(x => x.GetRandomTrainingWindow(_windowDuration), Times.Exactly(15));
        
        // verify formatter received arrays of the correct size
        _mockFormatter.Verify(x => x.Format(It.Is<IReadOnlyList<RawTrainingWindow>>(list => list.Count == 3)), Times.Exactly(5));
    }

    [Fact]
    public void GetValidationBatches_ValidConfig_ChunksCorrectlyAndFlushesRemainder()
    {
        // arrange
        int batchSize = 4;
        var dummyWindow = new RawTrainingWindow(Array.Empty<float[]>(), Array.Empty<AbsoluteEvent>(), TimeSpan.Zero, TimeSpan.Zero);
        var validationStream = Enumerable.Repeat(dummyWindow, 10);
        
        _mockDataLoader.Setup(x => x.GetSequentialWindows(_windowDuration, _stride)).Returns(validationStream);
        
        // capture list size immediately to avoid evaluating cleared reference later
        var capturedSizes = new List<int>();
        _mockFormatter.Setup(x => x.Format(It.IsAny<IReadOnlyList<RawTrainingWindow>>()))
            .Callback<IReadOnlyList<RawTrainingWindow>>(list => capturedSizes.Add(list.Count))
            .Returns(FormattedLabeledBatch.Empty);

        var sut = new StreamingDataLoader(
            _mockDataLoader.Object, 
            _mockFormatter.Object, 
            batchSize, 
            batchesPerEpoch: 1, 
            _stride, 
            _windowDuration);

        // act
        var batches = sut.GetValidationBatches().ToList();

        // assert
        batches.Should().HaveCount(3);
        
        // verify exactly two full batches and one remainder flush
        capturedSizes.Should().BeEquivalentTo(new[] { 4, 4, 2 }, options => options.WithStrictOrdering());
    }
}