using Maldact.Core.Data;

namespace Maldact.Core.Preprocessing;

/// <summary>
/// Defines the standard contract for all discrete signal processing (DSP) filters and transforms.
/// Implementations process sequential, time-series data chunks in a zero-allocation streaming pipeline.
/// </summary>
public interface IDataPreprocessor
{
    /// <summary>
    /// The expected number of features (variables) per time step in the incoming data chunk.
    /// </summary>
    int InputDimension { get; }

    /// <summary>
    /// The resulting number of features per time step after the processing step is applied.
    /// </summary>
    int OutputDimension { get; }

    /// <summary>
    /// Processes a sequential chunk of time-series data using memory pooling. 
    /// Maintains internal state across sequential calls to ensure continuous streaming continuity.
    /// Advances the pipeline chunk's internal buffer to securely hold the transformed features.
    /// </summary>
    /// <param name="chunk">The zero-allocation data carrier to be processed.</param>
    /// <exception cref="ArgumentNullException">Thrown if the chunk is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the chunk has already passed the preprocessing stage.</exception>
    void Process(PipelineChunk chunk);
}