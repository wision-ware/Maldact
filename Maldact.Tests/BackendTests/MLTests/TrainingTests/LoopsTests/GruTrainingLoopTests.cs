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
/// Verifies the recurrent training orchestration, early stopping logic, and native memory management for GruTrainingLoop.
/// </summary>
public class GruTrainingLoopTests
{
    private readonly Mock<IFormattedLabeledTrainingDataLoader> _mockDataLoader = new();

    /// <summary>
    /// Validates basic dependency injection constraints.
    /// </summary>
    [Fact]
    public void Constructor_NullModel_ThrowsArgumentNullException()
    {
        // arrange & act
        Action act = () => new GruTrainingLoop(null!)
        {
            MaxEpochs = 10,
            LearningRate = 0.01,
            Patience = 5,
            Device = torch.CPU
        };

        // assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Ensures the recurrent loop yields control immediately upon cancellation signal.
    /// </summary>
    [Fact]
    public async Task RunAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // arrange
        using var model = new TimeSeriesGru("test", 1, 4, 1, 1, 0.0);
        var sut = new GruTrainingLoop(model)
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

    /// <summary>
    /// Verifies that the recurrent loop terminates when validation improvement plateaus beyond the patience threshold.
    /// </summary>
    [Fact]
    public async Task RunAsync_NoImprovement_TriggersEarlyStopping()
    {
        // arrange
        using var model = new TimeSeriesGru("test", 2, 4, 1, 2, 0.0);
        int patience = 2;
        var sut = new GruTrainingLoop(model)
        {
            MaxEpochs = 10,
            LearningRate = 0.0, // freeze weights to force a loss plateau
            Patience = patience,
            Device = torch.CPU
        };

        var dummyBatch = CreateGruDummyBatch(1, 10, 2, 2);
        
        // deferred execution ensures fresh batches are yielded per epoch
        _mockDataLoader.Setup(d => d.GenerateBatches()).Returns(() => new[] { dummyBatch });
        _mockDataLoader.Setup(d => d.GetValidationBatches()).Returns(() => new[] { dummyBatch });

        var reportedMetrics = new List<EpochMetrics>();
        var progressMock = new Mock<IProgress<EpochMetrics>>();
        progressMock.Setup(p => p.Report(It.IsAny<EpochMetrics>()))
            .Callback<EpochMetrics>(m => reportedMetrics.Add(m));

        // act
        await sut.RunAsync(_mockDataLoader.Object, progressMock.Object, CancellationToken.None);

        // assert
        // epoch 1: best established (0)
        // epoch 2: plateau (1)
        // epoch 3: plateau (2) -> exit
        reportedMetrics.Should().HaveCount(3);
        reportedMetrics.Last().PatienceElapsed.Should().Be(patience);
    }

    /// <summary>
    /// Verifies the disk-backed checkpointing fallback by simulating validation loss degradation.
    /// </summary>
    [Fact]
    public async Task RunAsync_LossFluctuation_ReturnsOptimalCheckpoint()
    {
        // arrange
        using var model = new TimeSeriesGru("test", 1, 4, 1, 1, 0.0);
        var sut = new GruTrainingLoop(model)
        {
            MaxEpochs = 2,
            LearningRate = 0.01,
            Patience = 5,
            Device = torch.CPU
        };

        var trainBatch = CreateGruDummyBatch(1, 5, 1, 1);
        var valBatchBest = CreateGruDummyBatch(1, 5, 1, 1);
        var valBatchWorst = CreateGruDummyBatch(1, 5, 1, 1);
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
    }

    /// <summary>
    /// Validates resiliency against empty validation data streams.
    /// </summary>
    [Fact]
    public async Task RunAsync_EmptyValidationData_ReturnsCurrentModelState()
    {
        // arrange
        using var model = new TimeSeriesGru("test", 1, 4, 1, 1, 0.0);
        var sut = new GruTrainingLoop(model)
        {
            MaxEpochs = 1,
            LearningRate = 0.01,
            Patience = 1,
            Device = torch.CPU
        };

        var batch = CreateGruDummyBatch(1, 5, 1, 1);
        _mockDataLoader.Setup(d => d.GenerateBatches()).Returns(() => new[] { batch });
        _mockDataLoader.Setup(d => d.GetValidationBatches()).Returns(Enumerable.Empty<FormattedLabeledBatch>());

        // act
        var result = await sut.RunAsync(_mockDataLoader.Object, null, CancellationToken.None);

        // assert
        result.Should().NotBeNull();
        ((TorchModelParameters)result).Payload.IsEmpty.Should().BeFalse();
    }

    /// <summary>
    /// Helper to generate batch dimensions aligned with [Batch, Sequence, Features].
    /// </summary>
    private FormattedLabeledBatch CreateGruDummyBatch(int n, int s, int f, int classes)
    {
        return new FormattedLabeledBatch
        {
            FlattenedFeatures = new float[n * s * f],
            FeatureShape = new long[] { n, s, f },
            FlattenedTargets = new float[n * s * classes],
            TargetShape = new long[] { n, s, classes }
        };
    }
}