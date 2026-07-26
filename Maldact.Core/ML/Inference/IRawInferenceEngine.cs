using Maldact.Core.Data;

namespace Maldact.Core.ML.Inference;

/// <summary>
/// Defines the contract for an underlying machine learning model that processes raw feature frames into probability distributions.
/// </summary>
public interface IRawInferenceEngine : IDisposable
{
    /// <summary>
    /// Classifies a sequential window of preprocessed feature data into class probabilities.
    /// Safely transitions the pipeline chunk to the <see cref="ChunkState.Inferred"/> state.
    /// </summary>
    /// <param name="chunk">The zero-allocation data carrier containing the preprocessed features.</param>
    /// <exception cref="ArgumentNullException">Thrown if the chunk is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the chunk is not in a valid state for inference.</exception>
    void Classify(PipelineChunk chunk);
}