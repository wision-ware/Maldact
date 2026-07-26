using FluentAssertions;
using Maldact.Backend.ML.Consolidation.Tuning;
using Maldact.Core.Data;
using Maldact.Core.ML;
using Maldact.Core.ML.Consolidation.Tuning;
using Maldact.Core.ML.Inference;
using Maldact.Core.ML.Training;
using Maldact.Core.Results;

namespace Maldact.Tests.BackendTests.MLTests.ConsolidationTests.TuningTests;

/// <summary>
/// Verifies the disk-spooling mechanics, metric calculations (IoU / F1), and zero-allocation stateful orchestration of the consolidator tuning loop.
/// </summary>
public class ConsolidatorTuningLoopTests
{
    private readonly ClassificationClass _targetClass = new("TargetA");

    [Fact]
    public async Task RunAsync_MultipleCandidates_ReturnsHighestScoringConfiguration()
    {
        var gtEvent = new AbsoluteEvent
        {
            Class = _targetClass,
            StartOffset = TimeSpan.FromSeconds(1),
            EndOffset = TimeSpan.FromSeconds(2)
        };

        var dataLoader = new StubDataLoader(new[] { gtEvent });
        var inferenceEngine = new StubInferenceEngine();

        // candidate 1 yields nothing (0 true positives -> F1 = 0)
        var badConfig = new DummyConfiguration { Name = "Bad", ClassNames = [_targetClass.ClassName], FrameRateHz = 100};
        var badConsolidator = new StubConsolidator(badConfig, Array.Empty<ResultEntry>());

        // candidate 2 yields a perfect match (1 true positive -> F1 = 1.0)
        var goodConfig = new DummyConfiguration { Name = "Good", ClassNames = [_targetClass.ClassName], FrameRateHz = 100 };
        var goodConsolidator = new StubConsolidator(goodConfig, new[]
        {
            new ResultEntry(_targetClass, new StreamTime("00:00:01"), new StreamTime("00:00:02"), new StreamTime("00:00:01.5"), 0.9f, "id1")
        });

        var optimizer = new StubOptimizer(badConsolidator, goodConsolidator);
        var sut = new ConsolidatorTuningLoop(inferenceEngine, maxIterations: 5);

        var bestConfig = await sut.RunAsync(dataLoader, optimizer);

        bestConfig.Should().BeOfType<DummyConfiguration>();
        ((DummyConfiguration)bestConfig).Name.Should().Be("Good", "the optimizer should select the candidate with the 1.0 F1 score.");
    }

    [Fact]
    public async Task RunAsync_NoCandidatesYielded_ThrowsInvalidOperationException()
    {
        var sut = new ConsolidatorTuningLoop(new StubInferenceEngine(), maxIterations: 5);
        var emptyOptimizer = new StubOptimizer(); 

        Func<Task> act = async () => await sut.RunAsync(new StubDataLoader(Array.Empty<AbsoluteEvent>()), emptyOptimizer);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no candidates*");
    }

    [Fact]
    public async Task RunAsync_SubThresholdIoU_ScoresAsZeroF1()
    {
        var gtEvent = new AbsoluteEvent
        {
            Class = _targetClass,
            StartOffset = TimeSpan.FromSeconds(1),
            EndOffset = TimeSpan.FromSeconds(2)
        };

        // prediction is [1.8s -> 2.8s]. overlap is 0.2s. union is 1.8s. IoU = 0.11 (below 0.5)
        var badMatchConfig = new DummyConfiguration { Name = "BadMatch", ClassNames = ["TargetA"], FrameRateHz = 100 };
        var badMatchConsolidator = new StubConsolidator(badMatchConfig, new[]
        {
            new ResultEntry(_targetClass, new StreamTime("1.8"), new StreamTime("2.8"), new StreamTime("2.3"), 0.9f, "id1")
        });

        var optimizer = new StubOptimizer(badMatchConsolidator);
        var sut = new ConsolidatorTuningLoop(new StubInferenceEngine(), maxIterations: 1);

        var progressMetrics = new List<TuningMetrics>();
        var progress = new Progress<TuningMetrics>(progressMetrics.Add);

        await sut.RunAsync(new StubDataLoader(new[] { gtEvent }), optimizer, progress);

        progressMetrics.Should().ContainSingle();
        var metric = progressMetrics.First();
        
        metric.LatestScore.Should().Be(0f, "the IoU was below 0.5, meaning 0 true positives.");
    }

    [Fact]
    public async Task RunAsync_CancellationRequested_ThrowsTaskCanceledException()
    {
        var sut = new ConsolidatorTuningLoop(new StubInferenceEngine(), maxIterations: 5);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () => await sut.RunAsync(
            new StubDataLoader(Array.Empty<AbsoluteEvent>()), 
            new StubOptimizer(), 
            token: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- STUBS ---

    private class StubDataLoader : IRawContinuousLabeledDataLoader
    {
        private readonly AbsoluteEvent[] _groundTruth;

        public StubDataLoader(AbsoluteEvent[] groundTruth) => _groundTruth = groundTruth;

        public IEnumerable<float[][]> GetContinuousWindows()
        {
            // yield one dummy chunk to trigger the memory marshalling and binary writer
            yield return new[] { new[] { 0.5f, 0.5f } };
        }

        public AbsoluteEvent[] GetGroundTruthEvents() => _groundTruth;
    }

    private class StubInferenceEngine : IRawInferenceEngine
    {
        public void Classify(PipelineChunk chunk)
        {
            // Act as a strict pass-through for the pipeline lifecycle
            ReadOnlySpan<float> input = chunk.CurrentData;
            Span<float> output = chunk.TransitionToInference(input.Length);
            input.CopyTo(output);
        }
        
        public void Dispose() { }
    }

    private class StubOptimizer : IConsolidatorOptimizer
    {
        private readonly Queue<ITunableResultConsolidator> _candidates;

        public StubOptimizer(params ITunableResultConsolidator[] candidates)
        {
            _candidates = new Queue<ITunableResultConsolidator>(candidates);
        }

        public ITunableResultConsolidator? SuggestNext(float? score)
        {
            return _candidates.Count > 0 ? _candidates.Dequeue() : null;
        }
    }

    private class StubConsolidator : ITunableResultConsolidator
    {
        private readonly ConsolidatorConfiguration _config;
        private readonly ResultEntry[] _fixedResults;

        public StubConsolidator(ConsolidatorConfiguration config, ResultEntry[] fixedResults)
        {
            _config = config;
            _fixedResults = fixedResults;
        }

        public void Consolidate(PipelineChunk chunk)
        {
            // Consume chunk and yield fixed mock array
            chunk.TransitionToConsolidated(_fixedResults);
        }
        
        public ConsolidatorConfiguration GetConsolidatorConfiguration() => _config;
    }

    private record DummyConfiguration : ConsolidatorConfiguration
    {
        public string Name { get; set; } = string.Empty;
    }
}