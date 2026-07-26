using Maldact.Core.ML.Training;

namespace Maldact.Backend.ML.Training.BatchFormatting;

/// <summary>
/// Formats raw time-series windows into 2D tensors specifically tailored for 
/// Tree-based models (e.g., Random Forest, XGBoost) or fully connected Dense Neural Networks.
/// </summary>
public class TreeLabeledBatchFormatter : ILabeledBatchFormatter
{
    private readonly string[] _classes;
    private readonly double _windowDurationMs;

    /// <summary>
    /// Initializes a new instance of the TreeLabeledBatchFormatter.
    /// </summary>
    /// <param name="classes">The array of classification labels.</param>
    /// <param name="windowDuration">The fixed temporal duration of every window in the batch.</param>
    public TreeLabeledBatchFormatter(string[] classes, TimeSpan windowDuration)
    {
        ArgumentNullException.ThrowIfNull(classes);
        if (classes.Length == 0) throw new ArgumentException("At least one class must be defined.", nameof(classes));

        _classes = classes;
        _windowDurationMs = windowDuration.TotalMilliseconds;
    }

    /// <summary>
    /// Flattens a batch of 2D windows into contiguous 1D memory arrays representing 2D tensors.
    /// Time and feature dimensions are squashed together.
    /// Shape: [BatchSize, SequenceLength * FeatureDimension].
    /// </summary>
    /// <param name="rawBatch">The list of raw training windows to format.</param>
    /// <returns>A formatted batch containing the flattened features, window-level targets, and their 2D shapes.</returns>
    public FormattedLabeledBatch Format(IReadOnlyList<RawTrainingWindow> rawBatch)
    {
        ArgumentNullException.ThrowIfNull(rawBatch);
        if (rawBatch.Count == 0) return FormattedLabeledBatch.Empty;

        int batchSize = rawBatch.Count;
        int seqLen = rawBatch[0].Features.Length;
        int featureDim = seqLen > 0 ? rawBatch[0].Features[0].Length : 0; 

        float[] flatFeatures = new float[batchSize * seqLen * featureDim];
        
        for (int b = 0; b < batchSize; b++)
        {
            var window = rawBatch[b];

            // Validate against jagged time dimensions
            if (window.Features.Length != seqLen)
                throw new InvalidOperationException($"Jagged batch detected: Window {b} has {window.Features.Length} timesteps, expected {seqLen}.");

            for (int s = 0; s < seqLen; s++)
            {
                // Validate against jagged feature dimensions
                if (window.Features[s].Length != featureDim)
                    throw new InvalidOperationException($"Jagged batch detected: Window {b}, step {s} has {window.Features[s].Length} features, expected {featureDim}.");
                
                // Modern span copy for linear sequence processing
                window.Features[s].AsSpan().CopyTo(
                    flatFeatures.AsSpan((b * seqLen * featureDim) + (s * featureDim), featureDim)
                );
            }
        }

        return new FormattedLabeledBatch
        {
            FlattenedFeatures = flatFeatures,
            FeatureShape = [batchSize, seqLen * featureDim],
            FlattenedTargets = TargetLabelingMath.CalculateWindowTargets(rawBatch, _classes, _windowDurationMs),
            TargetShape = [batchSize, _classes.Length]
        };
    }
}