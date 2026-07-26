using System.Collections.Immutable;

namespace Maldact.Backend.Diagnostics;

/// <summary>
/// Encapsulates a strictly immutable, point-in-time snapshot of the server's health, memory footprint, and active sessions.
/// </summary>
/// <param name="ServerStartTime">The timestamp when the server process was initialized.</param>
/// <param name="ActiveStreamingSessions">The current count of actively connected telemetry streams.</param>
/// <param name="HangingRepositories">The count of allocated data repositories lacking an active network connection.</param>
/// <param name="TotalCachedResults">The aggregate number of machine learning results currently held in managed memory.</param>
/// <param name="EstimatedMemoryUsageBytes">The estimated heap byte footprint of the server cache.</param>
/// <param name="Sessions">The detailed state snapshots of all tracked slots.</param>
public sealed record ServerDiagnosticMetrics(
    DateTimeOffset ServerStartTime,
    int ActiveStreamingSessions,
    int HangingRepositories,
    long TotalCachedResults,
    long EstimatedMemoryUsageBytes,
    IReadOnlyList<StreamDiagnostic> Sessions)
{
    /// <inheritdoc />
    public bool Equals(ServerDiagnosticMetrics? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        
        return ServerStartTime.Equals(other.ServerStartTime) &&
               ActiveStreamingSessions == other.ActiveStreamingSessions &&
               HangingRepositories == other.HangingRepositories &&
               TotalCachedResults == other.TotalCachedResults &&
               EstimatedMemoryUsageBytes == other.EstimatedMemoryUsageBytes &&
               Sessions.SequenceEqual(other.Sessions); // enforces true structural equality
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ServerStartTime);
        hash.Add(ActiveStreamingSessions);
        hash.Add(HangingRepositories);
        hash.Add(TotalCachedResults);
        hash.Add(EstimatedMemoryUsageBytes);
        
        // order-dependent hashing for sequence
        foreach (var session in Sessions)
            hash.Add(session);
            
        return hash.ToHashCode();
    }
}