using Maldact.Core.Data;
using Maldact.Core.Results;

namespace Maldact.Core.ML.Inference;

//// <summary>
/// Defines a stateful, high-level ML inference pipeline that ingests zero-allocation chunks and outputs consolidated events.
/// Implements IDisposable to ensure underlying unmanaged tensor memory is safely flushed per session.
/// </summary>
public interface IInferenceEngine : IDisposable
{
    /// <summary>
    /// The expected number of features per temporal frame.
    /// </summary>
    int InputDimension { get; }

    /// <summary>
    /// Asynchronously processes a sequential chunk of feature data, orchestrating DSP, ML prediction, and temporal consolidation.
    /// </summary>
    /// <param name="chunk">The state-machine driven data carrier initialized with raw temporal frames.</param>
    /// <param name="ct">A cancellation token to safely abort the inference operation and trigger chunk disposal.</param>
    /// <returns>An array of consolidated, discrete events finalized during this chunk.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the chunk is null.</exception>
    Task<ResultEntry[]> ClassifyAsync(PipelineChunk chunk, CancellationToken ct = default);
}