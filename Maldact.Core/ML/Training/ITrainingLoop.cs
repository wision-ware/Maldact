using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.Data;

namespace Maldact.Core.ML.Training;

/// <summary>
/// Defines the contract for an orchestration loop that manages the full model training lifecycle.
/// </summary>
public interface ITrainingLoop
{
    /// <summary>
    /// Executes the training and validation loops using the provided data stream.
    /// </summary>
    /// <param name="dataLoader">The data engine yielding formatted tensor batches.</param>
    /// <param name="progress">An optional callback for reporting epoch-level performance metrics.</param>
    /// <param name="cancellationToken">A token to gracefully abort the long-running training computation.</param>
    /// <returns>The serialized parameters of the fully trained model.</returns>
    Task<IModelParameters> RunAsync(
        IFormattedLabeledTrainingDataLoader dataLoader, 
        IProgress<EpochMetrics>? progress = null,
        CancellationToken cancellationToken = default);
}