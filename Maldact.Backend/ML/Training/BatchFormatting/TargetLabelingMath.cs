using Maldact.Core.ML.Training;


namespace Maldact.Backend.ML.Training.BatchFormatting;

/// <summary>
/// Provides mathematical utilities for converting raw timestamped ground truth events 
/// into flat 1D floating-point arrays suitable for Machine Learning tensor ingestion.
/// </summary>
public static class TargetLabelingMath
{
    /// <summary>
    /// Calculates a flat multi-hot encoded array representing the presence of classes 
    /// anywhere within the duration of each window. Suitable for fully connected or CNN window-level classifiers.
    /// </summary>
    /// <param name="rawBatch">The list of formatted time windows containing ground truth events.</param>
    /// <param name="classes">The ordered array of class names mapping to tensor indices.</param>
    /// <param name="windowDurationMs">The duration of each window in milliseconds.</param>
    /// <returns>A flat 1D array of shape [batch_size * num_classes].</returns>
    public static float[] CalculateWindowTargets(
        IReadOnlyList<RawTrainingWindow> rawBatch, 
        string[] classes, 
        double windowDurationMs)
    {
        ArgumentNullException.ThrowIfNull(rawBatch);
        ArgumentNullException.ThrowIfNull(classes);
        if (windowDurationMs <= 0) throw new ArgumentOutOfRangeException(nameof(windowDurationMs), "Window duration must be greater than zero.");

        int numClasses = classes.Length;
        float[] flatTargets = new float[rawBatch.Count * numClasses];

        var classLookup = BuildClassLookup(classes);

        for (int b = 0; b < rawBatch.Count; b++)
        {
            var window = rawBatch[b];
            double windowStart = window.StartOffset.TotalMilliseconds;
            double windowEnd = windowStart + windowDurationMs;

            foreach (var ev in window.GroundTruthEvents)
            {
                if (windowEnd > ev.StartOffset.TotalMilliseconds && windowStart < ev.EndOffset.TotalMilliseconds)
                {
                    if (classLookup.TryGetValue(ev.Class.ClassName, out int classIdx))
                    {
                        flatTargets[(b * numClasses) + classIdx] = 1.0f;
                    }
                }
            }
        }
        return flatTargets;
    }

    /// <summary>
    /// Calculates a flat multi-hot encoded sequence array representing the presence of classes 
    /// at specific granular timesteps within each window. Suitable for RNN, LSTM, or Transformer sequence classifiers.
    /// </summary>
    /// <param name="rawBatch">The list of formatted time windows containing ground truth events.</param>
    /// <param name="classes">The ordered array of class names mapping to tensor indices.</param>
    /// <param name="windowDurationMs">The duration of each window in milliseconds.</param>
    /// <param name="seqLen">The number of granular timesteps to divide the window into.</param>
    /// <returns>A flat 1D array of shape [batch_size * seq_len * num_classes].</returns>
    public static float[] CalculateSequenceTargets(
        IReadOnlyList<RawTrainingWindow> rawBatch, 
        string[] classes, 
        double windowDurationMs, 
        int seqLen)
    {
        ArgumentNullException.ThrowIfNull(rawBatch);
        ArgumentNullException.ThrowIfNull(classes);
        if (windowDurationMs <= 0) throw new ArgumentOutOfRangeException(nameof(windowDurationMs), "Window duration must be greater than zero.");
        if (seqLen <= 0) throw new ArgumentOutOfRangeException(nameof(seqLen), "Sequence length must be greater than zero.");

        int batchSize = rawBatch.Count;
        int numClasses = classes.Length;
        float[] flatTargets = new float[batchSize * seqLen * numClasses];

        var classLookup = BuildClassLookup(classes);
        double msPerTimestep = windowDurationMs / seqLen;
        
        for (int b = 0; b < batchSize; b++)
        {
            var window = rawBatch[b];
            double windowStartMs = window.StartOffset.TotalMilliseconds;
            double windowEndMs = windowStartMs + windowDurationMs;

            foreach (var ev in window.GroundTruthEvents)
            {
                double evStart = ev.StartOffset.TotalMilliseconds;
                double evEnd = ev.EndOffset.TotalMilliseconds;

                if (evEnd <= windowStartMs || evStart >= windowEndMs) continue;

                if (!classLookup.TryGetValue(ev.Class.ClassName, out int classIdx)) continue;
                
                int startStep = Math.Max(0, (int)((evStart - windowStartMs) / msPerTimestep));
                int endStep = Math.Min(seqLen - 1, (int)((evEnd - windowStartMs) / msPerTimestep));
                
                for (int step = startStep; step <= endStep; step++)
                {
                    flatTargets[(b * seqLen * numClasses) + (step * numClasses) + classIdx] = 1.0f;
                }
            }
        }
        return flatTargets;
    }

    private static Dictionary<string, int> BuildClassLookup(string[] classes)
    {
        var lookup = new Dictionary<string, int>(classes.Length, StringComparer.Ordinal);
        for (int i = 0; i < classes.Length; i++)
        {
            lookup[classes[i]] = i;
        }
        return lookup;
    }
}