using Maldact.Backend.ML.Consolidation;
using Maldact.Core.ML;
using Maldact.Core.ML.Consolidation;
using Maldact.Core.ML.Consolidation.Tuning;
using Maldact.Core.Results;

namespace Maldact.Backend.ML.Builders;

/// <summary>
/// Provides extension methods to construct stateful temporal consolidators from configuration DTOs.
/// </summary>
public static class ConsolidatorBuilderExtensions
{
    /// <summary>
    /// Builds the temporal consolidator engine based on the provided configuration strategy.
    /// </summary>
    /// <param name="configuration">The polymorphic configuration payload defining the consolidation strategy.</param>
    /// <param name="sessionStartTime">The absolute chronological origin point of the stream.</param>
    /// <returns>An initialized consolidator ready to process probability frames.</returns>
    /// <exception cref="ArgumentNullException">Throws if configuration is null.</exception>
    /// <exception cref="ArgumentException">Throws if configuration is invalid.</exception>
    /// <exception cref="NotSupportedException">Throws if there is an unsupported configuration type being used.</exception>
    public static IResultConsolidator BuildConsolidator(
        this ConsolidatorConfiguration configuration, 
        StreamTime sessionStartTime)
    {
        if (configuration == null) 
            throw new ArgumentNullException(nameof(configuration));
            
        if (configuration.ClassNames == null || configuration.ClassNames.Length == 0)
            throw new ArgumentException("Consolidator configuration must define at least one target classification class.", nameof(configuration));

        var classes = configuration.ClassNames
            .Select(name => new ClassificationClass(name))
            .ToArray();
        
        return configuration switch
        {
            BasicConfiguration basic => 
                new ThresholdAttentionResultConsolidator(
                    configuration.FrameRateHz, 
                    sessionStartTime, 
                    classes, 
                    basic.Threshold,
                    basic.HangFrames),

            SlidingWindowConfiguration window => 
                new SlidingWindowHysteresisResultConsolidator(
                    configuration.FrameRateHz, 
                    sessionStartTime, 
                    window.WindowDuration, 
                    classes, 
                    window.ActivationDensity, 
                    window.DeactivationDensity),

            ExponentialConfiguration exp => 
                new ExponentialHysteresisResultConsolidator(
                    configuration.FrameRateHz, 
                    sessionStartTime, 
                    classes, 
                    exp.Alpha, 
                    exp.ActivationDensity, 
                    exp.DeactivationDensity),

            _ => throw new NotSupportedException($"Unknown consolidator strategy: {configuration.GetType().Name}")
        };
    }
}