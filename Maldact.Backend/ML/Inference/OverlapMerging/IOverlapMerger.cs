namespace Maldact.Backend.ML.Inference.OverlapMerging;

/// <summary>
/// Defines a zero-allocation contract for resolving probabilities when temporal sliding windows overlap.
/// </summary>
public interface IOverlapMerger
{
    /// <summary>
    /// Merges the probabilities of a previously unresolved tail frame into the current incoming head frame.
    /// Mutates the head frame in-place.
    /// </summary>
    /// <param name="newHeadFrame">The newly predicted probabilities for a specific chronological frame.</param>
    /// <param name="unresolvedTailFrame">The historical probabilities for the exact same chronological frame.</param>
    void Merge(Span<float> newHeadFrame, ReadOnlySpan<float> unresolvedTailFrame);
}