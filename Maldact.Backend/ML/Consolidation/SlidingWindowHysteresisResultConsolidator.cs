using Maldact.Backend.Server.Streaming;
using Maldact.Core.Data;
using Maldact.Core.ML;
using Maldact.Core.ML.Consolidation;
using Maldact.Core.ML.Consolidation.Tuning;
using Maldact.Core.Results;

namespace Maldact.Backend.ML.Consolidation;

/// <summary>
/// Implements standard arithmetic summation for discrete sliding windows.
/// </summary>
public struct SlidingWindowScorer : IWindowScorer
{
    private float _accumulator;

    /// <inheritdoc />
    public float CalculateScore(float incomingValue, float exitingValue)
    {
        _accumulator += incomingValue - exitingValue;
        return _accumulator;
    }
}

/// <summary>
/// Aggregates frame predictions into events using a sliding temporal window and density-based hysteresis bounds.
/// </summary>
public class SlidingWindowHysteresisResultConsolidator : ITunableResultConsolidator
{
    private readonly SlidingWindowConsolidationEngine<SlidingWindowScorer> _engine;
    
    private readonly float _activationDensity;
    private readonly float _deactivationDensity;
    private readonly double _frameRateHz;
    private readonly TimeSpan _windowDuration;
    private readonly ClassificationClass[] _classes;

    /// <summary>
    /// Initializes a new instance of the sliding window hysteresis consolidator.
    /// </summary>
    /// <param name="sampleRateHz">The sampling frequency of the incoming prediction frames.</param>
    /// <param name="sessionStartTime">The chronological start point of the data stream.</param>
    /// <param name="windowDuration">The chronological length of the trailing window to monitor.</param>
    /// <param name="classes">The array of target classes mapped to the inference output indices.</param>
    /// <param name="activationDensity">The required density of positive frames in the window to activate an event.</param>
    /// <param name="deactivationDensity">The density threshold at which an active event closes.</param>
    public SlidingWindowHysteresisResultConsolidator(
        double sampleRateHz,
        StreamTime sessionStartTime,
        TimeSpan windowDuration,
        ClassificationClass[] classes,
        float activationDensity,
        float deactivationDensity)
    {
        _classes = classes;
        _frameRateHz = sampleRateHz;
        _windowDuration = windowDuration;
        _activationDensity = activationDensity;
        _deactivationDensity = deactivationDensity;

        var windowSizeSamples = Math.Max(1, (int)(windowDuration.TotalSeconds * sampleRateHz));
        var activationThreshold = activationDensity * windowSizeSamples;
        var deactivationThreshold = deactivationDensity * windowSizeSamples;

        _engine = new SlidingWindowConsolidationEngine<SlidingWindowScorer>(
            sampleRateHz, sessionStartTime, windowSizeSamples, classes, 
            activationThreshold, deactivationThreshold, new SlidingWindowScorer());
    }

    /// <summary>
    /// Processes a sequential chunk of inference predictions via the sliding window engine using pooled memory.
    /// </summary>
    /// <param name="chunk">The zero-allocation data carrier in the Inferred state.</param>
    public void Consolidate(PipelineChunk chunk)
    {
        var results = _engine.Consolidate(chunk.CurrentData);
        chunk.TransitionToConsolidated(results);
    }

    /// <summary>
    /// Retrieves the DTO representing the active configuration of the sliding window consolidator.
    /// </summary>
    /// <returns>The populated sliding window configuration object.</returns>
    public ConsolidatorConfiguration GetConsolidatorConfiguration() => new SlidingWindowConfiguration
    {
        WindowDuration = _windowDuration,
        ActivationDensity = _activationDensity,
        DeactivationDensity = _deactivationDensity,
        ClassNames = _classes.Select(x => x.ClassName).ToArray(),
        FrameRateHz = _frameRateHz
    };
}