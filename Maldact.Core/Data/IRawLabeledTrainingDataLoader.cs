using Maldact.Core.ML;
using Maldact.Core.ML.Training;

namespace Maldact.Core.Data;

/// <summary>
/// Defines the contract for randomly sampling or sequentially iterating through labeled dataset windows.
/// </summary>
public interface IRawLabeledTrainingDataLoader
{
    /// <summary>
    /// The predefined classes this dataset maps targets against.
    /// </summary>
    ClassificationClass[] Classes { get; }
    
    /// <summary>
    /// The base sampling frequency (in Hertz) of the underlying binary streams.
    /// </summary>
    double SampleRateHz { get; }
    
    /// <summary>
    /// Samples a random temporal window of the specified duration from the training streams.
    /// Implementations must be thread-safe to support concurrent multi-worker batch compilation.
    /// </summary>
    /// <param name="windowDuration">The fixed temporal length of the requested window.</param>
    /// <returns>A raw training window containing the loaded features and intersecting ground truth events.</returns>
    RawTrainingWindow GetRandomTrainingWindow(TimeSpan windowDuration);
    
    /// <summary>
    /// Iterates through the validation streams using a sliding window approach.
    /// </summary>
    /// <param name="windowDuration">The fixed temporal length of each window.</param>
    /// <param name="stride">The temporal distance to advance the window between consecutive iterations.</param>
    /// <returns>An enumerable sequence of identically sized training windows.</returns>
    IEnumerable<RawTrainingWindow> GetSequentialWindows(TimeSpan windowDuration, TimeSpan stride);
}