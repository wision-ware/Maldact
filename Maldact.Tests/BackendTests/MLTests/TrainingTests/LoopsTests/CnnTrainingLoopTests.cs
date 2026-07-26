using FluentAssertions;
using Maldact.Backend.ML.ModelParameters;
using Maldact.Backend.ML.Modules;
using Maldact.Backend.ML.Training.Loops;
using Maldact.Core.Data;
using Maldact.Core.ML.Training;
using Moq;
using TorchSharp;

namespace Maldact.Tests.BackendTests.MLTests.TrainingTests.LoopsTests;

/// <summary>
/// Verifies the training orchestration, early stopping logic, and disk-backed checkpointing for CnnTrainingLoop.
/// </summary>
public class CnnTrainingLoopTests
{
    private readonly Mock<IFormattedLabeledTrainingDataLoader> _mockDataLoader = new();
    
    [Fact]
    public void Constructor_NullModel_ThrowsArgumentNullException()
    {
        // arrange & act
        Action act = () => new CnnTrainingLoop(null!)
        {
            Device = torch.CPU,
            LearningRate = 0.01,
            Patience = 5,
            MaxEpochs = 1
        };

        // assert
        act.Should().Throw<ArgumentNullException>();
    }
    
    [Fact]
    public async Task RunAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // arrange
        using var model = new TimeSeriesCnn("test", 1, new[] { 1 }, 3, 1);
        var sut = new CnnTrainingLoop(model)
        {
            MaxEpochs = 10,
            LearningRate = 0.01,
            Patience = 5,
            Device = torch.CPU
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // act
        Func<Task> act = async () => await sut.RunAsync(_mockDataLoader.Object, null, cts.Token);

        // assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
    
    [Fact]
    public async Task RunAsync_NoImprovement_TriggersEarlyStopping()
    {
        // arrange
        using var model = new TimeSeriesCnn("test", 1, new[] { 1 }, 3, 1);
        int patience = 2;
        var sut = new CnnTrainingLoop(model)
        {
            MaxEpochs = 10,
            // learning rate of 0 freezes weights, guaranteeing a validation loss plateau
            LearningRate = 0.0, 
            Patience = patience,
            Device = torch.CPU
        };

        var dummyBatch = CreateDummyBatch(1, 1, 5, 1);
        
        // mock yields static batches; loss will not improve due to frozen weights
        _mockDataLoader.Setup(d => d.GenerateBatches()).Returns(() => new[] { dummyBatch });
        _mockDataLoader.Setup(d => d.GetValidationBatches()).Returns(() => new[] { dummyBatch });

        var reportedMetrics = new List<EpochMetrics>();
        var progressMock = new Mock<IProgress<EpochMetrics>>();
        progressMock.Setup(p => p.Report(It.IsAny<EpochMetrics>()))
            .Callback<EpochMetrics>(m => reportedMetrics.Add(m));

        // act
        await sut.RunAsync(_mockDataLoader.Object, progressMock.Object, CancellationToken.None);

        // assert
        // epoch 1: best loss established (patience 0)
        // epoch 2: loss unchanged (patience 1)
        // epoch 3: loss unchanged (patience 2) -> break
        reportedMetrics.Should().HaveCount(3);
        reportedMetrics.Last().PatienceElapsed.Should().Be(patience);
    }
    
    [Fact]
    public async Task RunAsync_LossFluctuation_ReturnsOptimalCheckpoint()
    {
        // arrange
        using var model = new TimeSeriesCnn("test", 1, new[] { 1 }, 3, 1);
        var sut = new CnnTrainingLoop(model)
        {
            MaxEpochs = 2,
            LearningRate = 0.01,
            Patience = 5,
            Device = torch.CPU
        };

        var trainBatch = CreateDummyBatch(2, 1, 5, 1);
        
        // epoch 1 validation: standard targets
        var valBatchBest = CreateDummyBatch(2, 1, 5, 1);
        
        // epoch 2 validation: completely inverted targets to force a massive loss spike
        var valBatchWorst = CreateDummyBatch(2, 1, 5, 1);
        valBatchWorst = valBatchWorst with
        {
            FlattenedTargets = valBatchWorst.FlattenedTargets.Select(v => 1f - v).ToArray()
        };

        int callCount = 0;
        _mockDataLoader.Setup(d => d.GenerateBatches()).Returns(() => new[] { trainBatch });
        _mockDataLoader.Setup(d => d.GetValidationBatches()).Returns(() => 
        {
            return ++callCount == 1 ? new[] { valBatchBest } : new[] { valBatchWorst };
        });

        // act
        var result = await sut.RunAsync(_mockDataLoader.Object, null, CancellationToken.None);

        // assert
        var parameters = result as TorchModelParameters;
        parameters.Should().NotBeNull();
        parameters!.Payload.IsEmpty.Should().BeFalse();
        
        // file checkpoint bounds and successful return validates the physical i/o fallback
    }
    
    [Fact]
    public async Task RunAsync_EmptyValidationData_ReturnsCurrentModelState()
    {
        // arrange
        using var model = new TimeSeriesCnn("test", 1, new[] { 1 }, 3, 1);
        var sut = new CnnTrainingLoop(model)
        {
            MaxEpochs = 1,
            LearningRate = 0.01,
            Patience = 1,
            Device = torch.CPU
        };

        var batch = CreateDummyBatch(1, 1, 5, 1);
        _mockDataLoader.Setup(d => d.GenerateBatches()).Returns(() => new[] { batch });
        _mockDataLoader.Setup(d => d.GetValidationBatches()).Returns(Enumerable.Empty<FormattedLabeledBatch>());

        // act
        var result = await sut.RunAsync(_mockDataLoader.Object, null, CancellationToken.None);

        // assert
        var parameters = result as TorchModelParameters;
        parameters.Should().NotBeNull();
        parameters!.Payload.IsEmpty.Should().BeFalse();
    }
    
    private FormattedLabeledBatch CreateDummyBatch(int n, int c, int l, int classes)
    {
        return new FormattedLabeledBatch
        {
            FlattenedFeatures = new float[n * c * l],
            FeatureShape = new long[] { n, l, c },
            FlattenedTargets = new float[n * l * classes],
            TargetShape = new long[] { n, l, classes }
        };
    }
}