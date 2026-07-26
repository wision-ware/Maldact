using FluentAssertions;
using Maldact.Backend.ML.Inference;
using Maldact.Core.Data;
using Maldact.Core.ML.Consolidation;
using Maldact.Core.ML.Inference;
using Maldact.Core.Preprocessing;
using Maldact.Core.Results;

namespace Maldact.Tests.BackendTests.MLTests.InferenceTests;

/// <summary>
/// Verifies the execution order, cooperative cancellation, and zero-allocation routing 
/// of the top-level inference orchestrator.
/// </summary>
public class InferenceEngineTests
{
    [Fact]
    public void Constructor_NullDependencies_ThrowsArgumentNullException()
    {
        var raw = new SpyRawEngine(new List<string>());
        var consolidator = new SpyConsolidator(new List<string>());
        var pipeline = new SpyPreprocessor(new List<string>());

        FluentActions.Invoking(() => new InferenceEngine(null!, consolidator, pipeline))
            .Should().Throw<ArgumentNullException>();

        FluentActions.Invoking(() => new InferenceEngine(raw, null!, pipeline))
            .Should().Throw<ArgumentNullException>();

        FluentActions.Invoking(() => new InferenceEngine(raw, consolidator, null!))
            .Should().Throw<ArgumentNullException>();
    }
    
    [Fact]
    public void InputDimension_PassesThroughFromPreprocessor()
    {
        var pipeline = new SpyPreprocessor(new List<string>()) { InputDimension = 42 };
        var sut = new InferenceEngine(new SpyRawEngine(new List<string>()), new SpyConsolidator(new List<string>()), pipeline);

        sut.InputDimension.Should().Be(42, "the orchestrator must proxy the physical shape constraints of the DSP filter.");
    }
    
    [Fact]
    public async Task ClassifyAsync_ExecutesPipelineInStrictOrder()
    {
        var executionLog = new List<string>();
        var sut = new InferenceEngine(
            new SpyRawEngine(executionLog), 
            new SpyConsolidator(executionLog), 
            new SpyPreprocessor(executionLog));

        using var chunk = PipelineChunk.Rent(1, 10);

        var results = await sut.ClassifyAsync(chunk);

        executionLog.Should().ContainInOrder(
            "Preprocess",
            "Classify",
            "Consolidate"
        );
        
        results.Should().NotBeNull();
    }
    
    [Fact]
    public async Task ClassifyAsync_CancellationRequested_AbortsPipelineSafely()
    {
        var executionLog = new List<string>();
        var sut = new InferenceEngine(
            new SpyRawEngine(executionLog), 
            new SpyConsolidator(executionLog), 
            new SpyPreprocessor(executionLog));

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // trip the token before execution
        
        using var chunk = PipelineChunk.Rent(1, 10);

        Func<Task> act = async () => await sut.ClassifyAsync(chunk, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        executionLog.Should().BeEmpty("the pipeline must abort before invoking any heavy workloads.");
    }
    
    [Fact]
    public void Dispose_ForwardsToRawInferenceEngine()
    {
        var rawEngine = new SpyRawEngine(new List<string>());
        var sut = new InferenceEngine(rawEngine, new SpyConsolidator(new List<string>()), new SpyPreprocessor(new List<string>()));

        sut.Dispose();

        rawEngine.IsDisposed.Should().BeTrue("the orchestrator is responsible for flushing unmanaged tensor memory.");
    }
    
    private class SpyPreprocessor : IDataPreprocessor
    {
        private readonly List<string> _log;
        public int InputDimension { get; set; } = 10;
        public int OutputDimension => 10;

        public SpyPreprocessor(List<string> log) => _log = log;

        public void Process(PipelineChunk chunk)
        {
            _log.Add("Preprocess");
            // Mutate state so next step doesn't throw InvalidOperationException
            chunk.AdvancePreprocessingStep(chunk.CurrentData.Length);
        }
    }

    private class SpyRawEngine : IRawInferenceEngine
    {
        private readonly List<string> _log;
        public bool IsDisposed { get; private set; }

        public SpyRawEngine(List<string> log) => _log = log;

        public void Classify(PipelineChunk chunk)
        {
            _log.Add("Classify");
            // Mutate state to Inferred
            chunk.TransitionToInference(chunk.CurrentData.Length);
        }

        public void Dispose() => IsDisposed = true;
    }

    private class SpyConsolidator : IResultConsolidator
    {
        private readonly List<string> _log;

        public SpyConsolidator(List<string> log) => _log = log;

        public void Consolidate(PipelineChunk chunk)
        {
            _log.Add("Consolidate");
            // Mutate state to Consolidated
            chunk.TransitionToConsolidated(Array.Empty<ResultEntry>());
        }
    }
}