namespace Maldact.Core.ML.Training;

/// <summary>
/// A representation of a flattened tensor batch intended for supervised learning.
/// Appends ground truth target tensors to the base feature payload.
/// </summary>
public record FormattedLabeledBatch : FormattedBatch
{
    /// <summary>
    /// A static singleton representing a null or empty labeled batch.
    /// Hides the base Empty property to return the correctly typed subclass.
    /// </summary>
    public new static FormattedLabeledBatch Empty { get; } = new FormattedLabeledBatch
    {
        FlattenedFeatures = [],
        FeatureShape = [],
        FlattenedTargets = [],
        TargetShape = []
    };

    /// <summary>
    /// The contiguous 1D memory array holding all ground truth target data.
    /// </summary>
    public required float[] FlattenedTargets { get; init; }
    
    /// <summary>
    /// The N-dimensional shape required to reconstruct the target tensor.
    /// </summary>
    public required long[] TargetShape { get; init; }
}