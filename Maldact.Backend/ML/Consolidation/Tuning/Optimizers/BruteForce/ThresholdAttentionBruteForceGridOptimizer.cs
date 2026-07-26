using Maldact.Core.ML;
using Maldact.Core.ML.Consolidation.Tuning;
using Maldact.Core.Results;

namespace Maldact.Backend.ML.Consolidation.Tuning.Optimizers.BruteForce;

/// <summary>
/// Executes an exhaustive grid search over threshold attention consolidator parameters.
/// </summary>
public sealed class ThresholdAttentionBruteForceGridOptimizer : BruteForceGridOptimizer
{
    private static readonly float[] Thresholds = [0.4f, 0.5f, 0.6f, 0.7f, 0.8f];
    
    /// <summary>
    /// Initializes the threshold attention grid search optimizer.
    /// </summary>
    /// <param name="frameRateHz">The inference frame rate.</param>
    /// <param name="classes">The target class mapping.</param>
    public ThresholdAttentionBruteForceGridOptimizer(double frameRateHz, ClassificationClass[] classes) 
        : base(frameRateHz, classes) { }

    /// <inheritdoc />
    protected override IEnumerable<ITunableResultConsolidator> GenerateGrid()
    {
        return 
            from frames in FrameWindows 
            from thresh in Thresholds 
            select new ThresholdAttentionResultConsolidator(
                FrameRateHz,
                new StreamTime(TimeSpan.Zero),
                Classes,
                thresh,
                frames
            );
    }
}