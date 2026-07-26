
using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Maldact.Backend.Diagnostics;
using Maldact.Backend.Server.ControlProtocol;
using Maldact.Backend.Server.ControlProtocol.Responses;
using Maldact.Backend.Server.Streaming;
using Maldact.Core.ML.Inference;
using Maldact.Core.Results;
using Maldact.Core.Server.Authentication;

namespace Maldact.Backend.Server;

/// <summary>
/// Orchestrates the primary server lifecycle, secure client connections, control protocol routing, and telemetry reporting.
/// </summary>
public sealed class ServerRuntime
{
    public bool IsRemotelyKilled => _runtimeCts?.IsCancellationRequested == true;
    
    private const int MaxCommandLengthBytes = 8192; // 8KB sanity limit for control commands

    private readonly CommandRouter _router = new();
    private readonly StreamingManager _streamingManager;
    private readonly Func<IResultRepository> _createRepository;
    private readonly X509Certificate2 _certificate;
    private readonly TcpListener _controlTcpListener;
    private readonly IAuthenticator _authenticator;
    private readonly ConcurrentDictionary<AccessPass, StreamingSlot> _streamingSlots = new();
    private CancellationTokenSource? _runtimeCts = null;
    
    private ServerDiagnosticMetrics? _currentDiagnostics;
    
    /// <summary>
    /// Initializes a new instance of the ServerRuntime.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown if any required dependency is missing.</exception>
    public ServerRuntime(
        TcpListener controlTcpListener,
        TcpListener streamingTcpListener,
        X509Certificate2 certificate,
        IAuthenticator authenticator, 
        IInferenceEngineFactory inferenceEngineFactory,
        Func<IResultRepository> resultRepositoryFactory)
    {
        _controlTcpListener = controlTcpListener ?? throw new ArgumentNullException(nameof(controlTcpListener));
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
        _createRepository = resultRepositoryFactory ?? throw new ArgumentNullException(nameof(resultRepositoryFactory));
        _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
        
        _streamingManager = new StreamingManager(
            streamingTcpListener ?? throw new ArgumentNullException(nameof(streamingTcpListener)), 
            inferenceEngineFactory ?? throw new ArgumentNullException(nameof(inferenceEngineFactory)), 
            _certificate);
    }

    /// <summary>
    /// Starts the server listeners, background managers, and primary accept loop.
    /// </summary>
    /// <param name="cancellationToken">The host cancellation token.</param>
    /// <param name="progress">An optional provider for live telemetry updates.</param>
    /// <exception cref="InvalidOperationException">Thrown if attempted to be called the second time.</exception>
    public async Task RunAsync(CancellationToken cancellationToken = default, IProgress<ServerDiagnosticMetrics>? progress = null)
    {
        // establish a unified global CTS so admin stop commands successfully terminate the accept loop
        var newCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        if (Interlocked.CompareExchange(ref _runtimeCts, newCts, null) != null)
        {
            newCts.Dispose();
            throw new InvalidOperationException("This ServerRuntime instance is already running.");
        }
        
        _controlTcpListener.Start();
        
        var managerTask = _streamingManager.StartAsync(_runtimeCts.Token);
        var telemetryTask = RunTelemetryLoopAsync(_runtimeCts.Token, progress);
        
        try
        {
            while (!_runtimeCts.Token.IsCancellationRequested)
            {
                try
                {
                    var client = await _controlTcpListener.AcceptTcpClientAsync(_runtimeCts.Token);
                    
                    // fire and forget safely routed behind a global error trap
                    _ = HandleClientSafeAsync(client, _runtimeCts);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // swallow transient accept failures (e.g., rapid connect/disconnect from port scanners)
                }
            }
        }
        finally
        {
            _controlTcpListener.Stop();
            
            // force teardown of background systems if not already cancelled
            if (!_runtimeCts.IsCancellationRequested)
            {
                await _runtimeCts.CancelAsync();
            }
            
            await Task.WhenAll(telemetryTask, managerTask);
        }
    }

    /// <summary>
    /// Wraps the client handler in a global try/catch to prevent isolated connection faults from crashing the server process.
    /// </summary>
    private async Task HandleClientSafeAsync(TcpClient client, CancellationTokenSource globalCts)
    {
        using var _ = client;
        try
        {
            await HandleClientAsync(client, globalCts);
        }
        catch (OperationCanceledException)
        {
            // ignore deliberate shutdown
        }
        catch (Exception)
        {
            // gracefully close connection on TLS failure or protocol violation
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationTokenSource globalCts)
    {
        await using var sslStream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
        
        await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = _certificate,
            ClientCertificateRequired = false,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
        }, globalCts.Token);

        var context = new SessionContext(
            _authenticator,
            _streamingManager,
            TryGetStreamingSlot,
            TryBookSlot,
            () => _currentDiagnostics,
            globalCts // passes the true server-wide token source
        );

        var buffer = new byte[1024]; // increased buffer for standard throughput
        var messageBuilder = new StringBuilder();

        while (!globalCts.Token.IsCancellationRequested)
        {
            int bytesRead = await sslStream.ReadAsync(buffer, globalCts.Token);
            if (bytesRead == 0) break; // client disconnected gracefully

            string dataRead = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            messageBuilder.Append(dataRead);

            // enforce max payload size to prevent OOM DOS attacks
            if (messageBuilder.Length > MaxCommandLengthBytes)
            {
                throw new InvalidOperationException("Client exceeded maximum command payload size.");
            }

            string currentData = messageBuilder.ToString();
            int newLineIndex;
            
            while ((newLineIndex = currentData.IndexOf('\n')) >= 0)
            {
                string message = currentData.Substring(0, newLineIndex).Trim();
                messageBuilder.Remove(0, newLineIndex + 1);
                currentData = messageBuilder.ToString();
                
                var response = await _router.ExecuteAsync(message, context);
                
                await sslStream.WriteAsync(response, globalCts.Token);
            }
        }
    }

    private bool TryGetStreamingSlot(AccessPass pass, out StreamingSlot? slot) =>
        _streamingSlots.TryGetValue(pass, out slot);

    private bool TryBookSlot(AccessPass pass, out StreamingSlot? bookedSlot)
    {
        if (_streamingSlots.TryGetValue(pass, out var existingSlot))
        {
            if (existingSlot.State is SlotState.Active or SlotState.Reserved)
            {
                bookedSlot = null;
                return false;
            }
        }

        var newSlot = new StreamingSlot(_createRepository());
        _streamingSlots.AddOrUpdate(pass, newSlot, (_, _) => newSlot);

        bookedSlot = newSlot;
        return true;
    }
    
    private async Task RunTelemetryLoopAsync(CancellationToken cancellationToken, IProgress<ServerDiagnosticMetrics>? progress)
    {
        var serverStartTime = DateTimeOffset.UtcNow;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int activeStreams = 0;
                int hangingRepos = 0;
                long totalResults = 0;
                
                var sessionDiagnostics = new List<StreamDiagnostic>();
                
                foreach (var slotPair in _streamingSlots)
                {
                    var slot = slotPair.Value;
                    long cachedCount = await slot.Repository.GetCountAsync();
                    totalResults += cachedCount;

                    if (slot.State == SlotState.Active) activeStreams++;
                    else hangingRepos++;

                    if (slot.RemoteEndpoint is null) continue;

                    sessionDiagnostics.Add(new StreamDiagnostic(
                        SessionPassId: slotPair.Key.Id.ToString(), // assumes SessionId structure
                        RemoteEndpoint: slot.RemoteEndpoint,
                        State: slot.State,
                        CachedResults: cachedCount,
                        Uptime: DateTimeOffset.UtcNow - (slot.ConnectedAt ?? DateTimeOffset.UtcNow)
                    ));
                }
                
                var estimatedMemory = totalResults * ResultEntry.EstimatedBytesAllocated; 

                var metrics = new ServerDiagnosticMetrics(
                    serverStartTime,
                    activeStreams,
                    hangingRepos,
                    totalResults,
                    estimatedMemory,
                    sessionDiagnostics
                );

                progress?.Report(metrics);
                _currentDiagnostics = metrics;

                await Task.Delay(500, cancellationToken); 
            }
        }
        catch (OperationCanceledException)
        {
            // gracefully exit loop
        }
    }
}