namespace Maldact.Backend.ML.Inference.OverlapMerging;

/// <summary>
/// An overlap resolution strategy that calculates the mathematical mean of conflicting probabilities.
/// </summary>
public readonly struct AverageMerger : IOverlapMerger
{
    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown if span lengths do not match.</exception>
    public void Merge(Span<float> head, ReadOnlySpan<float> tail)
    {
        if (head.Length != tail.Length)
            throw new ArgumentException("Span lengths must match to perform an overlap merge.");

        for (int c = 0; c < head.Length; c++)
        {
            head[c] = (head[c] + tail[c]) / 2f;
        }
    }
}