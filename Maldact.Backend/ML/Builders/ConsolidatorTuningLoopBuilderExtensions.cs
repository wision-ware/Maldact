using Maldact.Backend.ML.Consolidation.Tuning.Optimizers;
using Maldact.Backend.ML.Consolidation.Tuning.Optimizers.BruteForce;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.ML;
using Maldact.Core.ML.Consolidation.Tuning;

namespace Maldact.Backend.ML.Builders;

/// <summary>
/// Provides extension methods for constructing composite optimizer engines securely.
/// </summary>
public static class ConsolidatorOptimizerBuilderExtensions
{
    /// <summary>
    /// Builds a sequential composite optimizer from the selected algorithmic flags in the training configuration.
    /// </summary>
    /// <param name="trainingConfiguration">The configuration payload containing the algorithmic flags.</param>
    /// <param name="frameRateHz">The sampling frequency of the predictions.</param>
    /// <param name="classes">The array of target classes to optimize for.</param>
    /// <returns>An orchestrated optimizer that will sequentially exhaust the search spaces of the selected algorithms.</returns>
    /// <exception cref="ArgumentException">Thrown when no valid consolidation algorithms are selected.</exception>
    public static IConsolidatorOptimizer GetOptimizer(
        this TrainingConfiguration trainingConfiguration, 
        double frameRateHz, 
        ClassificationClass[] classes)
    {
        if (trainingConfiguration == null) throw new ArgumentNullException(nameof(trainingConfiguration));
        if (classes == null || classes.Length == 0) throw new ArgumentException("At least one classification class must be provided.", nameof(classes));

        var optimizers = new List<IConsolidatorOptimizer>();
        var flags = trainingConfiguration.ConsolidationAlgorithms;

        if (flags.HasFlag(TrainingConfiguration.ConsolidationAlgorithm.BasicAttention))
        {
            optimizers.Add(new ThresholdAttentionBruteForceGridOptimizer(frameRateHz, classes));
        }

        if (flags.HasFlag(TrainingConfiguration.ConsolidationAlgorithm.ExponentialHysteresis))
        {
            optimizers.Add(new ExponentialHysteresisBruteForceGridOptimizer(frameRateHz, classes));
        }

        if (flags.HasFlag(TrainingConfiguration.ConsolidationAlgorithm.SlidingWindowHysteresis))
        {
            optimizers.Add(new SlidingWindowHysteresisBruteForceGridOptimizer(frameRateHz, classes));
        }

        if (optimizers.Count == 0)
        {
            throw new ArgumentException("Training configuration must specify at least one valid consolidation algorithm to optimize.", nameof(trainingConfiguration));
        }

        return new SequentialCompositeOptimizer(optimizers.ToArray());
    }
}