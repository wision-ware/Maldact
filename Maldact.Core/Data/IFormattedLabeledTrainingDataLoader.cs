using Maldact.Core.ML.Training;

namespace Maldact.Core.Data;

/// <summary>
/// Defines the contract for an orchestrator that pulls raw time-series data 
/// and maps it through a formatter to yield ML-ready tensor batches.
/// </summary>
public interface IFormattedLabeledTrainingDataLoader
{
    /// <summary>
    /// Yields a continuous sequence of randomly sampled training batches.
    /// </summary>
    /// <returns>An enumerable of formatted tensor batches.</returns>
    IEnumerable<FormattedLabeledBatch> GenerateBatches();
    
    /// <summary>
    /// Yields a sequential, sliding-window sequence of cross-validation batches.
    /// </summary>
    /// <returns>An enumerable of formatted tensor batches.</returns>
    IEnumerable<FormattedLabeledBatch> GetValidationBatches();
}