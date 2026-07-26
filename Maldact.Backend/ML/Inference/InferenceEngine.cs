using Maldact.Core.Data;
using Maldact.Core.ML.Consolidation;
using Maldact.Core.ML.Inference;
using Maldact.Core.Preprocessing;
using Maldact.Core.Results;

namespace Maldact.Backend.ML.Inference;

/// <summary>
/// The top-level orchestrator that executes the sequential ML pipeline: preprocessing, inference, and temporal consolidation.
/// Enforces cooperative cancellation, strict lifecycle validation, and zero-allocation processing.
/// </summary>
public sealed class InferenceEngine : IInferenceEngine
{
    private readonly IRawInferenceEngine _rawInferenceEngine;
    private readonly IResultConsolidator _resultConsolidator;
    private readonly IDataPreprocessor _pipeline;

    /// <summary>
    /// Initializes the top-level inference pipeline orchestrator.
    /// </summary>
    /// <param name="rawInferenceEngine">The underlying mathematical model evaluating raw frames.</param>
    /// <param name="resultConsolidator">The temporal engine that converts frame probabilities into discrete events.</param>
    /// <param name="pipeline">The preprocessing pipeline that scales and extracts features from raw sensor data.</param>
    /// <exception cref="ArgumentNullException">Thrown if any of the parameters are null.</exception>
    public InferenceEngine(
        IRawInferenceEngine rawInferenceEngine,
        IResultConsolidator resultConsolidator,
        IDataPreprocessor pipeline)
    {
        _rawInferenceEngine = rawInferenceEngine ?? throw new ArgumentNullException(nameof(rawInferenceEngine));
        _resultConsolidator = resultConsolidator ?? throw new ArgumentNullException(nameof(resultConsolidator));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    }

    /// <inheritdoc />
    public int InputDimension => _pipeline.InputDimension;

    /// <inheritdoc />
    public Task<ResultEntry[]> ClassifyAsync(PipelineChunk chunk, CancellationToken ct = default)
    {
        if (chunk == null) throw new ArgumentNullException(nameof(chunk));

        return Task.Run(() =>
        {
            // step 1: feature extraction via zero-allocation pipeline
            ct.ThrowIfCancellationRequested();
            _pipeline.Process(chunk);
            
            // step 2: model forward pass mutating the state to Inferred
            ct.ThrowIfCancellationRequested();
            _rawInferenceEngine.Classify(chunk);
            
            // step 3: consolidation and emission of discrete events
            ct.ThrowIfCancellationRequested();
            _resultConsolidator.Consolidate(chunk);
            
            return chunk.YieldFinal();
            
        }, ct);
    }

    /// <summary>
    /// Safely tears down the underlying unmanaged inference engine resources.
    /// </summary>
    public void Dispose()
    {
        _rawInferenceEngine.Dispose();
        GC.SuppressFinalize(this);
    }
}