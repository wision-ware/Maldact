using Maldact.Core.ML.Training;

namespace Maldact.Core.Data;

/// <summary>
/// Defines the contract for streaming continuous chunks of data from a dataset. 
/// Primarily used for unsupervised learning or whole-file continuous evaluation.
/// </summary>
public interface IRawContinuousLabeledDataLoader
{
    /// <summary>
    /// Yields contiguous, sequential chunks of features from the cross-validation streams.
    /// </summary>
    /// <returns>An enumerable of 2D arrays representing sequential feature chunks formatted as [Frames][Features].</returns>
    IEnumerable<float[][]> GetContinuousWindows();
    
    /// <summary>
    /// Retrieves all ground truth events across the evaluation dataset, mapped to a continuous cumulative timeline.
    /// </summary>
    /// <returns>An array of absolute events where timestamps represent the offset from the start of the concatenated stream sequence.</returns>
    AbsoluteEvent[] GetGroundTruthEvents();
}