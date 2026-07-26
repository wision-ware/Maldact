using Maldact.Core.ML.Training;

namespace Maldact.Backend.ML.Training.BatchFormatting;

/// <summary>
/// Formats raw time-series windows into 3D sequence tensors specifically tailored for 
/// Gated Recurrent Unit (GRU) or Long Short-Term Memory (LSTM) neural networks.
/// </summary>
public class GruLabeledBatchFormatter : ILabeledBatchFormatter
{
    private readonly string[] _classes;
    private readonly double _windowDurationMs;

    /// <summary>
    /// Initializes a new instance of the GruLabeledBatchFormatter.
    /// </summary>
    /// <param name="classes">The array of classification labels.</param>
    /// <param name="windowDuration">The fixed temporal duration of every window in the batch.</param>
    public GruLabeledBatchFormatter(string[] classes, TimeSpan windowDuration)
    {
        ArgumentNullException.ThrowIfNull(classes);
        if (classes.Length == 0) throw new ArgumentException("At least one class must be defined.", nameof(classes));

        _classes = classes;
        _windowDurationMs = windowDuration.TotalMilliseconds;
    }

    /// <summary>
    /// Flattens a batch of 2D windows into contiguous 1D memory arrays representing 3D tensors.
    /// Shape: [BatchSize, SequenceLength, FeatureDimension].
    /// </summary>
    /// <param name="rawBatch">The list of raw training windows to format.</param>
    /// <returns>A formatted batch containing the flattened features, targets, and their 3D shapes.</returns>
    public FormattedLabeledBatch Format(IReadOnlyList<RawTrainingWindow> rawBatch)
    {
        ArgumentNullException.ThrowIfNull(rawBatch);
        if (rawBatch.Count == 0) return FormattedLabeledBatch.Empty; // requires adding an Empty static property to FormattedLabeledBatch

        var batchSize = rawBatch.Count;
        var seqLen = rawBatch[0].Features.Length;
        var featureDim = seqLen > 0 ? rawBatch[0].Features[0].Length : 0;
        var numClasses = _classes.Length;
        
        var flatFeatures = new float[batchSize * seqLen * featureDim];

        for (var b = 0; b < batchSize; b++)
        {
            var window = rawBatch[b];

            // fail-fast structural validation to prevent memory corruption and silent tensor dimension errors
            if (window.Features.Length != seqLen)
                throw new InvalidOperationException($"Jagged batch detected: Window {b} has {window.Features.Length} timesteps, expected {seqLen}.");

            for (var s = 0; s < seqLen; s++)
            {
                if (window.Features[s].Length != featureDim)
                    throw new InvalidOperationException($"Jagged batch detected: Window {b}, step {s} has {window.Features[s].Length} features, expected {featureDim}.");

                // modern span copy avoids bounds checking overhead in hot loops
                window.Features[s].AsSpan().CopyTo(
                    flatFeatures.AsSpan((b * seqLen * featureDim) + (s * featureDim), featureDim)
                );
            }
        }

        return new FormattedLabeledBatch
        {
            FlattenedFeatures = flatFeatures,
            FeatureShape = [batchSize, seqLen, featureDim],
            FlattenedTargets = TargetLabelingMath.CalculateSequenceTargets(rawBatch, _classes, _windowDurationMs, seqLen),
            TargetShape = [batchSize, seqLen, numClasses]
        };
    }
}