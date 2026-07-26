using Maldact.Core.Results;

namespace Maldact.Backend.ML.Consolidation;

/// <summary>
/// Converts discrete 0-based frame indices into absolute or relative chronological timestamps.
/// </summary>
public class TimeStamper
{
    private readonly double _frameRateHz;
    private readonly StreamTime _t0;

    /// <summary>
    /// Initializes a new instance of the time stamper.
    /// </summary>
    /// <param name="frameRateHz">The sampling frequency of the underlying data stream.</param>
    /// <param name="t0">The origin timestamp representing frame 0.</param>
    public TimeStamper(double frameRateHz, StreamTime t0)
    {
        _frameRateHz = frameRateHz;
        _t0 = t0;
    }

    /// <summary>
    /// Calculates the chronological time of a specific frame index based on the stream's sample rate and origin.
    /// </summary>
    /// <param name="sampleIndex">The 0-based index of the target frame.</param>
    /// <returns>The calculated stream time.</returns>
    public StreamTime GetTime(long sampleIndex)
    {
        // delegate tick rounding to the framework to avoid floating-point truncation drift
        var offset = TimeSpan.FromSeconds(sampleIndex / _frameRateHz);

        // utilize the struct's internal operator overload to handle origin type branching
        return _t0 + offset;
    }
    
    /// <summary>
    /// Calculates the chronological time of a specific frame index based on the stream's sample rate and origin.
    /// </summary>
    /// <param name="sampleIndex">The 0-based index of the target frame.</param>
    /// <returns>The calculated stream time.</returns>
    public StreamTime GetTime(double sampleIndex)
    {
        // delegate tick rounding to the framework to avoid floating-point truncation drift
        var offset = TimeSpan.FromSeconds(sampleIndex / _frameRateHz);

        // utilize the struct's internal operator overload to handle origin type branching
        return _t0 + offset;
    }
}