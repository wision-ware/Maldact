using FluentAssertions;
using Maldact.Backend.ML.ModelParameters;
using Maldact.Backend.ML.Modules;
using Maldact.Backend.ML.Training.Loops;
using Maldact.Core.Data;
using Maldact.Core.ML.Training;
using Moq;

namespace Maldact.Tests.BackendTests.MLTests.TrainingTests.LoopsTests;

public class TreeTrainingLoopTests
{
    private readonly Mock<IFormattedLabeledTrainingDataLoader> _mockDataLoader = new();

    [Fact]
    public async Task RunAsync_StochasticSampling_DistributesMultiHotLabels()
    {
        // arrange
        int numClasses = 3;
        var sut = new TreeTrainingLoop
        {
            EnsembleType = TreeTrainingLoop.TreeType.RandomForest,
            NumberOfTrees = 1,
            MaxDepth = 2,
            NumClasses = numClasses,
            Seed = 42
        };

        var multiHotBatch = new FormattedLabeledBatch
        {
            FlattenedFeatures = new float[] { 0.1f, 0.2f },
            FeatureShape = new long[] { 1, 2 },
            FlattenedTargets = new float[] { 1.0f, 0.0f, 1.0f }, // multi-hot: [1, 0, 1]
            TargetShape = new long[] { 1, 3 }
        };

        var batchSequence = Enumerable.Repeat(multiHotBatch, 100);
        _mockDataLoader.Setup(d => d.GenerateBatches()).Returns(batchSequence);
        _mockDataLoader.Setup(d => d.GetValidationBatches()).Returns(batchSequence);

        // act
        var result = await sut.RunAsync(_mockDataLoader.Object);

        // assert
        result.Should().NotBeNull();
        result.Should().BeOfType<TreeModelParameters>();
        
        // expected exactly 2 calls: once for dynamic schema peeking, once for actual training unroll
        _mockDataLoader.Verify(d => d.GenerateBatches(), Times.Exactly(2));
    }

    [Fact]
    public async Task RunAsync_ProgressReporting_CallsReportOnStartAndFinish()
    {
        // arrange
        var sut = new TreeTrainingLoop
        {
            EnsembleType = TreeTrainingLoop.TreeType.RandomForest,
            NumberOfTrees = 1,
            MaxDepth = 1,
            NumClasses = 2
        };

        // to prevent the ML.NET evaluator from crashing, the dataset MUST contain at least 2 distinct classes.
        // we provide a batch of size 2: row 1 is Class 0, row 2 is Class 1.
        var dummyBatch = new FormattedLabeledBatch
        {
            FlattenedFeatures = new float[] { 0f, 0f }, // 2 rows, 1 feature
            FeatureShape = new long[] { 2, 1 },
            FlattenedTargets = new float[] { 1f, 0f,   0f, 1f }, // Row 1 = Class 0, Row 2 = Class 1
            TargetShape = new long[] { 2, 2 }
        };

        _mockDataLoader.Setup(d => d.GenerateBatches()).Returns(new[] { dummyBatch });
        _mockDataLoader.Setup(d => d.GetValidationBatches()).Returns(new[] { dummyBatch });

        var progressMock = new Mock<IProgress<EpochMetrics>>();
        var reportedMetrics = new List<EpochMetrics>();
        progressMock.Setup(p => p.Report(It.IsAny<EpochMetrics>()))
                    .Callback<EpochMetrics>(m => reportedMetrics.Add(m));

        // act
        await sut.RunAsync(_mockDataLoader.Object, progressMock.Object, CancellationToken.None);

        // assert
        reportedMetrics.Should().HaveCountGreaterThanOrEqualTo(2);
        reportedMetrics.First().Epoch.Should().Be(0);
        reportedMetrics.Last().Epoch.Should().Be(1);
    }

    [Fact]
    public void TreePrediction_Initialization_HasEmptyProbabilities()
    {
        // arrange & act
        var prediction = new TreePrediction();

        // assert
        prediction.Probabilities.Should().NotBeNull().And.BeEmpty();
    }
}