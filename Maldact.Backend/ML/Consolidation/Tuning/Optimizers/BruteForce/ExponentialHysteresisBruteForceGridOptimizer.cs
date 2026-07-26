using Maldact.Core.ML;
using Maldact.Core.ML.Consolidation.Tuning;
using Maldact.Core.Results;

namespace Maldact.Backend.ML.Consolidation.Tuning.Optimizers.BruteForce;

/// <summary>
/// Executes an exhaustive grid search over exponential moving average (EMA) hysteresis thresholds.
/// </summary>
public sealed class ExponentialHysteresisBruteForceGridOptimizer : BruteForceGridOptimizer
{
    // static readonly allocations eliminate gen0 pressure on rapid instantiation
    private static readonly float[] Activations = [0.5f, 0.6f, 0.7f, 0.8f];
    private static readonly float[] Deactivations = [0.1f, 0.2f, 0.3f, 0.4f];

    /// <summary>
    /// Initializes the EMA grid search optimizer.
    /// </summary>
    /// <param name="frameRateHz">The inference frame rate.</param>
    /// <param name="classes">The target class mapping.</param>
    public ExponentialHysteresisBruteForceGridOptimizer(double frameRateHz, ClassificationClass[] classes)
        : base(frameRateHz, classes) { }
    
    /// <inheritdoc />
    protected override IEnumerable<ITunableResultConsolidator> GenerateGrid()
    {
        return 
            from frames in FrameWindows 
            from act in Activations 
            from deact in Deactivations 
            where !(deact >= act) 
            let alpha = 2f / (frames + 1f) 
            select new ExponentialHysteresisResultConsolidator(
                FrameRateHz, 
                new StreamTime(TimeSpan.Zero), 
                Classes, 
                alpha, 
                act, 
                deact
            );
    }
}