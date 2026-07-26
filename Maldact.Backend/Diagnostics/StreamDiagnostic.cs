using Maldact.Backend.Server.Streaming;

namespace Maldact.Backend.Diagnostics;


/// <summary>
/// Encapsulates the network and storage telemetry for a single ingestion slot.
/// </summary>
/// <param name="SessionPassId">The unique authorization identifier bound to the slot.</param>
/// <param name="RemoteEndpoint">The IP address of the connected client, if bound.</param>
/// <param name="State">The current network lifecycle state of the slot.</param>
/// <param name="CachedResults">The number of results retained by this specific session's repository.</param>
/// <param name="Uptime">The duration the stream has been continuously active.</param>
public sealed record StreamDiagnostic(
    string SessionPassId,
    string RemoteEndpoint,
    SlotState State,
    long CachedResults,
    TimeSpan Uptime
);