using Maldact.Core.Data;
using Maldact.Core.ML.Inference;

namespace Maldact.Core.ML.Consolidation.Tuning;

/// <summary>
/// Orchestrates the iterative evaluation of consolidator hyperparameters against a labeled ground-truth dataset.
/// </summary>
public interface IConsolidatorTuningLoop
{
    /// <summary>
    /// Executes the sequential optimization sweep until convergence.
    /// </summary>
    /// <param name="dataLoader">The provider of chronological inference frames and ground-truth labels.</param>
    /// <param name="optimizer">The mathematical strategy guiding the hyperparameter search space.</param>
    /// <param name="progress">An optional callback to report ongoing iteration metrics.</param>
    /// <param name="token">A cancellation token to safely abort the tuning process.</param>
    /// <returns>The highest-scoring consolidator configuration discovered during the sweep.</returns>
    Task<ConsolidatorConfiguration> RunAsync(
        IRawContinuousLabeledDataLoader dataLoader, 
        IConsolidatorOptimizer optimizer,
        IProgress<TuningMetrics>? progress = null,
        CancellationToken token = default);
}