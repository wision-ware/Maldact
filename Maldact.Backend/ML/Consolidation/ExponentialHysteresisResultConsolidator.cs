using Maldact.Backend.Server.Streaming;
using Maldact.Core.Data;
using Maldact.Core.ML;
using Maldact.Core.ML.Consolidation.Tuning;
using Maldact.Core.Results;

namespace Maldact.Backend.ML.Consolidation;

/// <summary>
/// Implements exponential moving average (EMA) smoothing for delayed windows.
/// </summary>
public struct ExponentialScorer : IWindowScorer
{
    private readonly float _alpha;
    private float _accumulator;
    
    /// <summary>
    /// Initializes a new instance of the exponential scorer.
    /// </summary>
    /// <param name="alpha">The smoothing factor (0 to 1) dictating the decay rate.</param>
    public ExponentialScorer(float alpha)
    {
        _alpha = alpha;
        _accumulator = 0f;
    }

    /// <inheritdoc />
    public float CalculateScore(float incomingValue, float exitingValue) 
    {
        // ema disregards trailing buffer exits, only reacting to the incoming delta
        _accumulator = (incomingValue * _alpha) + (_accumulator * (1f - _alpha));
        return _accumulator;
    }
}

/// <summary>
/// Aggregates frame predictions into events using an exponential moving average (EMA) hysteresis threshold.
/// Incorporates a physical delay line to align event boundaries accurately without phase shift.
/// </summary>
public class ExponentialHysteresisResultConsolidator : ITunableResultConsolidator
{
    private readonly SlidingWindowConsolidationEngine<ExponentialScorer> _engine;
    
    private readonly float _activation;
    private readonly float _deactivation;
    private readonly float _alpha;
    private readonly double _frameRateHz;
    private readonly ClassificationClass[] _classes;

    /// <summary>
    /// Initializes a new instance of the exponential hysteresis consolidator.
    /// </summary>
    /// <param name="frameRateHz">The sampling frequency of the incoming prediction frames.</param>
    /// <param name="sessionStartTime">The chronological start point of the data stream.</param>
    /// <param name="classes">The array of target classes mapped to the inference output indices.</param>
    /// <param name="alpha">The smoothing factor (0 to 1) dictating the decay rate.</param>
    /// <param name="activationThreshold">The absolute EMA value required to activate an event.</param>
    /// <param name="deactivationThreshold">The absolute EMA value at which an active event closes.</param>
    public ExponentialHysteresisResultConsolidator(
        double frameRateHz, 
        StreamTime sessionStartTime, 
        ClassificationClass[] classes, 
        float alpha, 
        float activationThreshold, 
        float deactivationThreshold)
    {
        _classes = classes;
        _alpha = alpha;
        _activation = activationThreshold;
        _deactivation = deactivationThreshold;
        _frameRateHz = frameRateHz;

        var effectiveWindowSize = (2.0 / alpha) - 1.0;
        var windowSizeSamples = Math.Max(1, (int)Math.Round(effectiveWindowSize));

        _engine = new SlidingWindowConsolidationEngine<ExponentialScorer>(
            frameRateHz, sessionStartTime, windowSizeSamples, classes, 
            activationThreshold, deactivationThreshold, new ExponentialScorer(alpha));
    }

    /// <summary>
    /// Processes a sequential chunk of inference predictions via the exponential delayed engine using pooled memory.
    /// </summary>
    /// <param name="chunk">The zero-allocation data carrier in the Inferred state.</param>
    public void Consolidate(PipelineChunk chunk)
    {
        var results = _engine.Consolidate(chunk.CurrentData);
        chunk.TransitionToConsolidated(results);
    }

    /// <summary>
    /// Retrieves the DTO representing the active configuration of the exponential consolidator.
    /// </summary>
    /// <returns>The populated exponential configuration object.</returns>
    public ConsolidatorConfiguration GetConsolidatorConfiguration() => new ExponentialConfiguration
    {
        ActivationDensity = _activation,
        DeactivationDensity = _deactivation,
        ClassNames = _classes.Select(x => x.ClassName).ToArray(),
        Alpha = _alpha,
        FrameRateHz = _frameRateHz
    };
}