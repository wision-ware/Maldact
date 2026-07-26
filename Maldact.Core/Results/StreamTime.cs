using System.Globalization;

namespace Maldact.Core.Results;

/// <summary>
/// A dual-purpose Discriminated Union time primitive representing either an absolute wall-clock timestamp or a relative stream offset.
/// </summary>
public readonly struct StreamTime : IEquatable<StreamTime>, IComparable<StreamTime>
{
    private readonly long _ticks;
    
    /// <summary>
    /// Gets a value indicating whether this instance represents an absolute UTC timestamp.
    /// </summary>
    public bool IsAbsolute { get; }

    /// <summary>
    /// Gets a value indicating whether this instance represents a relative time duration.
    /// </summary>
    public bool IsRelative => !IsAbsolute;

    /// <summary>
    /// Initializes a new absolute stream time from a wall-clock timestamp.
    /// </summary>
    /// <param name="absoluteTime">The absolute wall-clock timestamp.</param>
    public StreamTime(DateTime absoluteTime)
    {
        _ticks = absoluteTime.ToUniversalTime().Ticks; 
        IsAbsolute = true;
    }

    /// <summary>
    /// Initializes a new relative stream time from a duration.
    /// </summary>
    /// <param name="relativeTime">The relative stream offset.</param>
    public StreamTime(TimeSpan relativeTime)
    {
        _ticks = relativeTime.Ticks;
        IsAbsolute = false;
    }

    /// <summary>
    /// Initializes a stream time by parsing a string representation.
    /// </summary>
    /// <param name="timeString">The serialized time string.</param>
    /// <exception cref="ArgumentException">Thrown when the input string is null or whitespace.</exception>
    /// <exception cref="FormatException">Thrown when the string cannot be parsed into a known absolute or relative format.</exception>
    public StreamTime(string timeString)
    {
        if (string.IsNullOrWhiteSpace(timeString))
            throw new ArgumentException("Time string cannot be empty.", nameof(timeString));
        
        if (double.TryParse(timeString, NumberStyles.Any, CultureInfo.InvariantCulture, out double ms))
        {
            _ticks = TimeSpan.FromMilliseconds(ms).Ticks;
            IsAbsolute = false;
            return;
        }

        if (TimeSpan.TryParse(timeString, CultureInfo.InvariantCulture, out TimeSpan ts))
        {
            _ticks = ts.Ticks;
            IsAbsolute = false;
            return;
        }
        
        if (DateTime.TryParse(timeString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dt))
        {
            _ticks = dt.ToUniversalTime().Ticks;
            IsAbsolute = true;
            return;
        }

        throw new FormatException($"Cannot parse '{timeString}' into a valid Absolute or Relative StreamTime.");
    }

    /// <summary>
    /// Extracts the absolute wall-clock representation.
    /// </summary>
    /// <returns>The absolute UTC DateTime.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this instance represents a relative time.</exception>
    public DateTime AsAbsolute()
    {
        if (!IsAbsolute) throw new InvalidOperationException("Cannot extract DateTime from a Relative StreamTime.");
        return new DateTime(_ticks, DateTimeKind.Utc);
    }

    /// <summary>
    /// Extracts the relative duration representation.
    /// </summary>
    /// <returns>The relative TimeSpan offset.</returns>
    /// <exception cref="InvalidOperationException">Thrown if this instance represents an absolute time.</exception>
    public TimeSpan AsRelative()
    {
        if (IsAbsolute) throw new InvalidOperationException("Cannot extract TimeSpan from an Absolute StreamTime.");
        return new TimeSpan(_ticks);
    }
    
    /// <summary>
    /// Gets the total milliseconds represented by a relative stream time.
    /// Optimized to prevent transient TimeSpan allocations.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if this instance represents an absolute time.</exception>
    public double TotalMilliseconds => IsAbsolute 
        ? throw new InvalidOperationException("Cannot get TotalMilliseconds from an Absolute StreamTime.") 
        : _ticks / (double)TimeSpan.TicksPerMillisecond;
    
    /// <summary>
    /// Adds a relative duration to the stream time.
    /// </summary>
    /// <param name="streamTime">The base stream time.</param>
    /// <param name="relativeTime">The duration to add.</param>
    /// <returns>The shifted stream time.</returns>
    public static StreamTime operator +(StreamTime streamTime, TimeSpan relativeTime) => streamTime.IsAbsolute 
        ? new StreamTime(streamTime.AsAbsolute().Add(relativeTime)) 
        : new StreamTime(streamTime.AsRelative().Add(relativeTime));
    
    /// <summary>
    /// Subtracts a relative duration from the stream time.
    /// </summary>
    /// <param name="streamTime">The base stream time.</param>
    /// <param name="relativeTime">The duration to subtract.</param>
    /// <returns>The shifted stream time.</returns>
    public static StreamTime operator -(StreamTime streamTime, TimeSpan relativeTime) => streamTime.IsAbsolute 
        ? new StreamTime(streamTime.AsAbsolute().Subtract(relativeTime)) 
        : new StreamTime(streamTime.AsRelative().Subtract(relativeTime));

    /// <summary>
    /// Calculates the duration between two compatible stream times.
    /// </summary>
    /// <param name="left">The later stream time.</param>
    /// <param name="right">The earlier stream time.</param>
    /// <returns>The time span between the two boundaries.</returns>
    /// <exception cref="InvalidOperationException">Thrown if modality types mismatch.</exception>
    public static TimeSpan operator -(StreamTime left, StreamTime right)
    {
        EnsureCompatible(left, right);
        return TimeSpan.FromTicks(left._ticks - right._ticks);
    }
    
    /// <summary>
    /// Validates that two stream times share the same absolute/relative modality.
    /// </summary>
    private static void EnsureCompatible(StreamTime a, StreamTime b)
    {
        if (a.IsAbsolute != b.IsAbsolute)
            throw new InvalidOperationException(
                $"Time format mismatch: Cannot compare an {(a.IsAbsolute ? "Absolute" : "Relative")} time with a {(b.IsAbsolute ? "Absolute" : "Relative")} time.");
    }
    
    public static bool operator <(StreamTime left, StreamTime right)
    {
        EnsureCompatible(left, right);
        return left._ticks < right._ticks;
    }
    
    public static bool operator >(StreamTime left, StreamTime right)
    {
        EnsureCompatible(left, right);
        return left._ticks > right._ticks;
    }
    
    public static bool operator <=(StreamTime left, StreamTime right)
    {
        EnsureCompatible(left, right);
        return left._ticks <= right._ticks;
    }

    public static bool operator >=(StreamTime left, StreamTime right)
    {
        EnsureCompatible(left, right);
        return left._ticks >= right._ticks;
    }
    
    public static bool operator ==(StreamTime left, StreamTime right) => 
        left.IsAbsolute == right.IsAbsolute && left._ticks == right._ticks;
    
    public static bool operator !=(StreamTime left, StreamTime right) => !(left == right);
    
    /// <inheritdoc />
    public int CompareTo(StreamTime other)
    {
        EnsureCompatible(this, other);
        return _ticks.CompareTo(other._ticks);
    }

    /// <inheritdoc />
    public bool Equals(StreamTime other) => this == other;
    
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is StreamTime other && Equals(other);
    
    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_ticks, IsAbsolute);

    /// <inheritdoc />
    public override string ToString() => IsAbsolute ? AsAbsolute().ToString("O") : $"{TotalMilliseconds} ms";
}