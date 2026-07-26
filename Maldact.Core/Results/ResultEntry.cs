using Maldact.Core.ML;

namespace Maldact.Core.Results;

/// <summary>
/// Encapsulates a confirmed prediction event extracted from the streaming inference pipeline.
/// </summary>
/// <param name="Classification">The matched target class.</param>
/// <param name="StartTime">The temporal start boundary of the event.</param>
/// <param name="EndTime">The temporal end boundary of the event.</param>
/// <param name="CentroidTime">The mathematically weighted center of mass for the event's confidence scores.</param>
/// <param name="Score">The mean confidence score aggregated over the event duration.</param>
/// <param name="Id">The unique identifier tracking this specific event.</param>
public sealed record ResultEntry(
    ClassificationClass Classification,
    StreamTime StartTime,
    StreamTime EndTime,
    StreamTime CentroidTime,
    float Score,
    string Id
)
{
    /// <summary>
    /// The estimated byte footprint of this object in managed memory.
    /// </summary>
    public const int EstimatedBytesAllocated = 96;
    
    /// <inheritdoc />
    public override string ToString() => $"[S{StartTime} - E{EndTime} | C{CentroidTime}] ID {Id} SCORE {Score} RESULT {Classification}";
}