namespace Maldact.Core.Server;

/// <summary>
/// Represents a unique, thread-safe identifier for a discrete server connection or session lifecycle.
/// </summary>
public sealed class SessionId : IEquatable<SessionId>
{
    // atomic counter for thread-safe generation across all concurrent connections
    private static long _nextId;
    
    /// <summary>
    /// Gets the underlying numeric value of the session identifier.
    /// </summary>
    public long Value { get; }

    /// <summary>
    /// Initializes a new session identifier via an atomic thread-safe increment.
    /// </summary>
    public SessionId()
    {
        Value = Interlocked.Increment(ref _nextId);
    }

    /// <inheritdoc />
    public bool Equals(SessionId? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Value == other.Value;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is SessionId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();
}