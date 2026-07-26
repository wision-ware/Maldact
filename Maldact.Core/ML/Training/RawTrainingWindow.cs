namespace Maldact.Core.ML.Training;

/// <summary>
/// Represents a discrete slice of time containing synchronized feature data and its corresponding ground truth events.
/// </summary>
public record RawTrainingWindow
{
    /// <summary>
    /// The 2D array of processed features representing [timeSteps][features].
    /// </summary>
    public float[][] Features { get; init; }
    
    /// <summary>
    /// An immutable collection of all events that intersect with this window's timeframe.
    /// </summary>
    public IReadOnlyList<AbsoluteEvent> GroundTruthEvents { get; init; }
    
    /// <summary>
    /// The starting timestamp of this window relative to the original raw recording.
    /// </summary>
    public TimeSpan StartOffset { get; init; }
    
    /// <summary>
    /// The ending timestamp of this window relative to the original raw recording.
    /// </summary>
    public TimeSpan EndOffset { get; init; }

    /// <summary>
    /// The total duration of this specific training window.
    /// </summary>
    public TimeSpan Duration => EndOffset - StartOffset;

    /// <summary>
    /// Initializes a new instance of the RawTrainingWindow, ensuring structural time integrity.
    /// </summary>
    public RawTrainingWindow(
        float[][] features, 
        IReadOnlyList<AbsoluteEvent> groundTruthEvents, 
        TimeSpan startOffset, 
        TimeSpan endOffset)
    {
        if (endOffset < startOffset) 
            throw new ArgumentException("EndOffset cannot be earlier than StartOffset.");

        Features = features;
        GroundTruthEvents = groundTruthEvents;
        StartOffset = startOffset;
        EndOffset = endOffset;
    }
}