using System.Text.Json.Serialization;

namespace Maldact.Core.ML.Consolidation.Tuning;

/// <summary>
/// The root configuration defining how frame-level predictions are aggregated over time.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$strategy")]
[JsonDerivedType(typeof(BasicConfiguration), "basic_attention")]
[JsonDerivedType(typeof(SlidingWindowConfiguration), "sliding_window_hysteresis")]
[JsonDerivedType(typeof(ExponentialConfiguration), "exponential_hysteresis")]
public abstract record ConsolidatorConfiguration
{
    /// <summary>
    /// The sampling rate of the incoming prediction stream.
    /// </summary>
    public required double FrameRateHz { get; init; }

    /// <summary>
    /// The human-readable labels corresponding to the output dimension indices.
    /// </summary>
    public required string[] ClassNames  { get; init; }
}

/// <summary>
/// Configuration for a simple threshold-based consolidation strategy.
/// </summary>
public record BasicConfiguration : ConsolidatorConfiguration
{
    /// <summary>
    /// The confidence threshold required to activate a prediction.
    /// </summary>
    public required float Threshold { get; init; }

    /// <summary>
    /// The number of frames to hold the prediction active after confidence drops below the threshold.
    /// </summary>
    public required int HangFrames { get; init; }
}

/// <summary>
/// Base configuration for hysteresis-based temporal smoothing strategies.
/// </summary>
public abstract record HysteresisConfiguration : ConsolidatorConfiguration
{
    /// <summary>
    /// The density threshold required to flip a class to active.
    /// </summary>
    public required float ActivationDensity { get; init; }

    /// <summary>
    /// The density threshold required to flip a class back to inactive.
    /// </summary>
    public required float DeactivationDensity { get; init; }
}

/// <summary>
/// Configuration for a sliding-window hysteresis consolidator.
/// </summary>
public record SlidingWindowConfiguration : HysteresisConfiguration
{
    /// <summary>
    /// The absolute duration of the historical window to monitor for activation densities.
    /// </summary>
    public required TimeSpan WindowDuration { get; init; }
}

/// <summary>
/// Configuration for an exponential decay hysteresis consolidator.
/// </summary>
public record ExponentialConfiguration : HysteresisConfiguration
{
    /// <summary>
    /// The decay factor determining how quickly historical activations lose their weight.
    /// </summary>
    public required float Alpha { get; init; } 
}