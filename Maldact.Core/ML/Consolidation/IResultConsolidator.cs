using Maldact.Core.Data;
using Maldact.Core.Results;

namespace Maldact.Core.ML.Consolidation;

/// <summary>
/// Defines the contract for aggregating frame-level machine learning predictions into discrete, time-bound events.
/// </summary>
public interface IResultConsolidator
{
    /// <summary>
    /// Processes a sequential chunk of inference predictions and transitions the chunk to a consolidated state.
    /// Safely triggers the release of the unmanaged memory buffer back to the system pool.
    /// </summary>
    /// <param name="chunk">The pipeline chunk containing raw probabilities in the Inferred state.</param>
    /// <exception cref="ArgumentNullException">Thrown if the chunk is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the chunk is not in the Inferred state.</exception>
    void Consolidate(PipelineChunk chunk);
}