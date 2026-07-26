namespace Maldact.Core.ML.Consolidation.Tuning;

/// <summary>
/// Defines a stateful hyperparameter optimization strategy that yields sequential consolidator configurations for evaluation.
/// </summary>
public interface IConsolidatorOptimizer
{
    /// <summary>
    /// Ingests the evaluation score of the previously yielded consolidator and suggests the next optimal configuration.
    /// </summary>
    /// <param name="score">The evaluation metric of the last iteration, or null if initializing.</param>
    /// <returns>An instantiated consolidator ready for evaluation, or null if the optimization has completed.</returns>
    ITunableResultConsolidator? SuggestNext(float? score);
}