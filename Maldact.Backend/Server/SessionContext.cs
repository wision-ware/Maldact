using Maldact.Backend.Diagnostics;
using Maldact.Backend.Server.Streaming;
using Maldact.Core.ML;
using Maldact.Core.ML.Inference;
using Maldact.Core.Results;
using Maldact.Core.Server;
using Maldact.Core.Server.Authentication;
using Maldact.Core.Streaming;

namespace Maldact.Backend.Server;

/// <summary>
/// Encapsulates the strict state, security identity, and runtime dependencies for an individual client session securely.
/// </summary>
internal class SessionContext
{
    /// <summary>
    /// Defines a delegate signature for retrieving an existing streaming slot based on access claims.
    /// </summary>
    public delegate bool SlotTryGetter(AccessPass accessPass, out StreamingSlot? slot);
    
    /// <summary>
    /// Defines a delegate signature for reserving a new streaming slot based on access claims.
    /// </summary>
    public delegate bool SlotTryBooker(AccessPass accessPass, out StreamingSlot? slot);

    private readonly CancellationTokenSource _runtimeCancellationTokenSource;

    /// <summary>
    /// Initializes a new, secure session context with validated dependencies.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown if any required dependency is missing.</exception>
    public SessionContext(
        IAuthenticator authenticator, 
        StreamingManager streamingManager,
        SlotTryGetter slotGetter,
        SlotTryBooker slotBooker,
        Func<ServerDiagnosticMetrics?> diagnosticsGetter,
        CancellationTokenSource runtimeCancellationTokenSource)
    {
        Authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
        StreamingManager = streamingManager ?? throw new ArgumentNullException(nameof(streamingManager));
        TryGetSlot = slotGetter ?? throw new ArgumentNullException(nameof(slotGetter));
        TryBookSlot = slotBooker ?? throw new ArgumentNullException(nameof(slotBooker));
        GetServerDiagnosticMetrics = diagnosticsGetter ?? throw new ArgumentNullException(nameof(diagnosticsGetter));
        _runtimeCancellationTokenSource = runtimeCancellationTokenSource ?? throw new ArgumentNullException(nameof(runtimeCancellationTokenSource));
        
        Id = new SessionId();
    }
    
    /// <summary>
    /// Gets the authentication engine bound to this session.
    /// </summary>
    public IAuthenticator Authenticator { get; }
    
    /// <summary>
    /// Gets the streaming manager responsible for routing raw network ingestion.
    /// </summary>
    public StreamingManager StreamingManager { get; }

    /// <summary>
    /// Gets the globally unique identifier for this session.
    /// </summary>
    public SessionId Id { get; }

    /// <summary>
    /// Gets the validated security pass for the current user, if authenticated.
    /// </summary>
    public AccessPass? AccessPass { get; private set; }
    
    /// <summary>
    /// Gets a value indicating whether the session has been successfully authenticated.
    /// </summary>
    public bool IsAuthenticated => AccessPass is not null;

    /// <summary>
    /// Gets the injected function used to retrieve active slots.
    /// </summary>
    public SlotTryGetter TryGetSlot { get; }

    /// <summary>
    /// Gets the injected function used to reserve new slots.
    /// </summary>
    public SlotTryBooker TryBookSlot { get; }
    
    /// <summary>
    /// Gets the injected function used to poll internal server telemetry.
    /// </summary>
    public Func<ServerDiagnosticMetrics?> GetServerDiagnosticMetrics { get; }
    
    /// <summary>
    /// Gets the cancellation token indicating when the server runtime is shutting down.
    /// </summary>
    public CancellationToken ServerCancellationToken => _runtimeCancellationTokenSource.Token;

    /// <summary>
    /// Authenticates the session, locking in the provided access claims.
    /// </summary>
    /// <param name="pass">The validated security pass.</param>
    /// <exception cref="InvalidOperationException">Thrown if the session is already authenticated.</exception>
    public void SetAuthenticatedState(AccessPass pass)
    {
        if (IsAuthenticated) throw new InvalidOperationException("Session is already authenticated.");
        AccessPass = pass ?? throw new ArgumentNullException(nameof(pass));
    }

    /// <summary>
    /// Issues a global termination request to the underlying server runtime.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Thrown if the session does not possess administrative privileges.</exception>
    public void RequestServerShutdown()
    {
        if (AccessPass?.AccessRole != AccessPass.Role.Admin)
            throw new UnauthorizedAccessException("Only administrators can initiate a server shutdown.");
        
        // time bomb to ensure response goes through before shutdown
        _ = Task.Run(async () => 
        {
            await Task.Delay(500);
            _runtimeCancellationTokenSource.Cancel();
        });
    }
}