namespace Maldact.Core.ML.Training;

/// <summary>
/// A base representation of a flattened tensor batch intended for unsupervised learning.
/// Contains only the raw features and their original N-dimensional shape.
/// </summary>
public record FormattedBatch
{
    /// <summary>
    /// A static singleton representing a null or empty batch to safely avoid null references.
    /// </summary>
    public static FormattedBatch Empty { get; } = new FormattedBatch
    {
        FlattenedFeatures = [],
        FeatureShape = []
    };

    /// <summary>
    /// The contiguous 1D memory array holding all feature data across the batch.
    /// </summary>
    public required float[] FlattenedFeatures { get; init; }
    
    /// <summary>
    /// The N-dimensional shape required to reconstruct the tensor (e.g., [BatchSize, SequenceLength, FeatureDimension]).
    /// </summary>
    public required long[] FeatureShape { get; init; }
}