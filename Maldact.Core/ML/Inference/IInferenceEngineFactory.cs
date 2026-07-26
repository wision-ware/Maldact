using Maldact.Core.Results;

namespace Maldact.Core.ML.Inference;

/// <summary>
/// Defines a factory for spawning stateful inference engine instances.
/// Typically registered as a Singleton to cache heavy model weights, spawning lightweight session-specific engines.
/// </summary>
public interface IInferenceEngineFactory
{
    /// <summary>
    /// Creates a new, stateful inference engine configured with its own temporal boundaries and consolidation buffers.
    /// </summary>
    /// <param name="sessionStartTime">The absolute chronological origin point of the target data stream.</param>
    /// <returns>A fully initialized, disposable inference engine ready to process incoming data chunks.</returns>
    IInferenceEngine Create(StreamTime sessionStartTime);
}