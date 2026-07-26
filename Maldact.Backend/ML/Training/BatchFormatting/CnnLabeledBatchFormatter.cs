using Maldact.Core.ML.Training;

namespace Maldact.Backend.ML.Training.BatchFormatting;

/// <summary>
/// Formats raw time-series windows into 3D sequence tensors specifically tailored for Torch
/// 1D Convolutional Neural Networks (CNNs). Transposes the spatial dimensions to match expected channel layouts.
/// </summary>
public class CnnLabeledBatchFormatter : ILabeledBatchFormatter
{
    private readonly string[] _classes;
    private readonly double _windowDurationMs;

    /// <summary>
    /// Initializes a new instance of the CnnLabeledBatchFormatter.
    /// </summary>
    /// <param name="classes">The array of classification labels.</param>
    /// <param name="windowDuration">The fixed temporal duration of every window in the batch.</param>
    public CnnLabeledBatchFormatter(string[] classes, TimeSpan windowDuration)
    {
        // check if classes are defined
        ArgumentNullException.ThrowIfNull(classes);
        if (classes.Length == 0) throw new ArgumentException("At least one class must be defined.", nameof(classes));
        
        _classes = classes;
        _windowDurationMs = windowDuration.TotalMilliseconds;
    }

    /// <summary>
    /// Flattens a batch of 2D windows into contiguous 1D memory arrays representing 3D tensors.
    /// Transposes the dimensions to shape: [BatchSize, FeatureDimension, SequenceLength].
    /// </summary>
    /// <param name="rawBatch">The list of raw training windows to format.</param>
    /// <returns>A formatted batch containing the flattened transposed features, targets, and their 3D shapes.</returns>
    public FormattedLabeledBatch Format(IReadOnlyList<RawTrainingWindow> rawBatch)
    {
        ArgumentNullException.ThrowIfNull(rawBatch);
        if (rawBatch.Count == 0) return FormattedLabeledBatch.Empty;
        
        int batchSize = rawBatch.Count;
        int seqLen = rawBatch[0].Features.Length;
        int featureDim = rawBatch[0].Features[0].Length; 

        float[] flatFeatures = new float[batchSize * seqLen * featureDim];
        
        for (int b = 0; b < batchSize; b++)
        {
            var window = rawBatch[b];
            if (window.Features.Length != seqLen)
                throw new InvalidOperationException($"Jagged batch: Window {b} has {window.Features.Length} timesteps, expected {seqLen}.");
            
            for (int s = 0; s < seqLen; s++)
            {
                var stepFeatures = window.Features[s];
                if (stepFeatures.Length != featureDim)
                    throw new InvalidOperationException($"Jagged batch: Window {b}, step {s} has {stepFeatures.Length} features, expected {featureDim}.");
                
                for (int f = 0; f < featureDim; f++)
                {
                    flatFeatures[(b * seqLen * featureDim) + (s * featureDim) + f] = stepFeatures[f];
                }
            }
        }

        return new FormattedLabeledBatch
        {
            FlattenedFeatures = flatFeatures,
            FeatureShape = [batchSize, seqLen, featureDim],
            FlattenedTargets = TargetLabelingMath.CalculateSequenceTargets(
                rawBatch, _classes, _windowDurationMs, seqLen),
            TargetShape = [batchSize, seqLen, _classes.Length]
        };
    }
}