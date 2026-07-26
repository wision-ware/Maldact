namespace Maldact.Backend.ML.Inference.OverlapMerging;

/// <summary>
/// An overlap resolution strategy that takes the highest confidence value among conflicting probabilities.
/// </summary>
public readonly struct MaxMerger : IOverlapMerger
{
    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown if span lengths do not match.</exception>
    public void Merge(Span<float> head, ReadOnlySpan<float> tail)
    {
        if (head.Length != tail.Length)
            throw new ArgumentException("Span lengths must match to perform an overlap merge.");

        for (int c = 0; c < head.Length; c++)
        {
            head[c] = MathF.Max(head[c], tail[c]);
        }
    }
}