namespace Maldact.Core.ML.Training;

/// <summary>
/// Defines the contract for transforming raw memory windows into flat ML tensors.
/// </summary>
public interface ILabeledBatchFormatter
{
    /// <summary>
    /// Formats a raw batch of chronological windows into flattened 1D arrays matching specific tensor shapes.
    /// </summary>
    /// <param name="rawBatch">The chronological list of raw memory windows.</param>
    /// <returns>A formatted labeled batch ready for ML ingestion.</returns>
    FormattedLabeledBatch Format(IReadOnlyList<RawTrainingWindow> rawBatch);
}