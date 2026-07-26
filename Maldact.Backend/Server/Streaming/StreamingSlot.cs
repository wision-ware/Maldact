using Maldact.Core.Results;
using Maldact.Core.Server.Authentication;

namespace Maldact.Backend.Server.Streaming;

/// <summary>
/// Defines the lifecycle stages of a streaming ingestion slot.
/// </summary>
public enum SlotState
{
    /// <summary>
    /// The slot is allocated and awaiting an incoming connection.
    /// </summary>
    Reserved = 0,
    
    /// <summary>
    /// The slot is currently bound to an active network stream.
    /// </summary>
    Active = 1,
    
    /// <summary>
    /// The stream has disconnected and holds captured data until a new stream tries to connect.
    /// </summary>
    Inactive = 2
}

/// <summary>
/// Represents a strictly stateful, thread-safe routing destination for an incoming telemetry stream.
/// </summary>
internal class StreamingSlot
{
    private int _state;

    /// <summary>
    /// Gets the target storage repository bound to this session.
    /// </summary>
    public IResultRepository Repository { get; }
    
    /// <summary>
    /// Gets the current atomic network lifecycle state of the slot.
    /// </summary>
    public SlotState State => (SlotState)_state;

    /// <summary>
    /// Gets the remote IP address of the connected client.
    /// </summary>
    public string? RemoteEndpoint { get; private set; }

    /// <summary>
    /// Gets the absolute UTC timestamp when the client successfully connected.
    /// </summary>
    public DateTimeOffset? ConnectedAt { get; private set; }

    /// <summary>
    /// Gets the absolute UTC timestamp when the client disconnected or the slot was terminated.
    /// </summary>
    public DateTimeOffset? DisconnectedAt { get; private set; }

    /// <summary>
    /// Initializes a newly reserved streaming slot.
    /// </summary>
    /// <param name="repository">The storage interface to bind to this session.</param>
    /// <exception cref="ArgumentNullException">Thrown if the repository is null.</exception>
    public StreamingSlot(IResultRepository repository)
    {
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _state = (int)SlotState.Reserved;
    }

    /// <summary>
    /// Atomically attempts to bind a remote client endpoint and transition the slot to an active state.
    /// </summary>
    /// <param name="endpoint">The IP address of the incoming connection.</param>
    /// <returns>True if the slot was exclusively locked and activated; false if it was already hijacked, active, or inactive.</returns>
    public bool TryActivate(string endpoint)
    {
        if (Interlocked.CompareExchange(ref _state, (int)SlotState.Active, (int)SlotState.Reserved) != (int)SlotState.Reserved)
        {
            return false;
        }

        RemoteEndpoint = endpoint;
        ConnectedAt = DateTimeOffset.UtcNow;
        
        return true;
    }

    /// <summary>
    /// Atomically transitions the slot to a permanently inactive state and records the termination timestamp.
    /// </summary>
    public void Deactivate()
    {
        if (Interlocked.CompareExchange(ref _state, (int)SlotState.Inactive, (int)SlotState.Active) == (int)SlotState.Active)
        {
            DisconnectedAt = DateTimeOffset.UtcNow;
        }
    }
}