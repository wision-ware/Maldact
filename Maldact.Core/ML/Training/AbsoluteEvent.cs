namespace Maldact.Core.ML.Training;

/// <summary>
/// Represents a distinct occurrence of a class bounded by a specific start and end time.
/// </summary>
public record AbsoluteEvent
{
    /// <summary>
    /// A unique identifier for tracking this specific event instance. Cached to prevent GC allocations.
    /// </summary>
    public string Id { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The labeled category of this event.
    /// </summary>
    public required ClassificationClass Class { get; init; }
    
    /// <summary>
    /// The exact timestamp when this event begins relative to the start of the recording.
    /// </summary>
    public required TimeSpan StartOffset { get; init; }
    
    /// <summary>
    /// The exact timestamp when this event concludes relative to the start of the recording.
    /// </summary>
    public required TimeSpan EndOffset { get; init; }
    
    /// <summary>
    /// The total elapsed time of the event.
    /// </summary>
    public TimeSpan Duration => EndOffset - StartOffset;
}