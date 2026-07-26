using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Maldact.Core.ML.Inference;
using Maldact.Core.Results;
using Maldact.Core.Streaming;

namespace Maldact.Backend.Server.Streaming;

/// <summary>
/// Securely manages the continuous ingestion of external TCP streams, handling strict TLS negotiation, session routing, and connection lifecycle.
/// </summary>
internal class StreamingManager
{
    private readonly ConcurrentDictionary<string, StreamingSlot> _pendingSessions = new();
    private readonly TcpListener _listener;
    private readonly IInferenceEngineFactory _inferenceFactory;
    private readonly X509Certificate2 _certificate;

    /// <summary>
    /// Gets the port the TCP listener is actively bound to.
    /// </summary>
    public int StreamingPort { get; }

    /// <summary>
    /// Initializes a new instance of the StreamingManager.
    /// </summary>
    /// <param name="listener">The underlying TCP listener.</param>
    /// <param name="inferenceFactory">The factory to instantiate session-specific inference engines.</param>
    /// <param name="certificate">The TLS certificate for securing inbound connections.</param>
    /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
    public StreamingManager(TcpListener listener, IInferenceEngineFactory inferenceFactory, X509Certificate2 certificate)
    {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _inferenceFactory = inferenceFactory ?? throw new ArgumentNullException(nameof(inferenceFactory));
        _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
        StreamingPort = ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>
    /// Starts the asynchronous connection acceptance loop without preemptive task cancellation.
    /// </summary>
    /// <param name="ct">The cancellation token to terminate the server loop.</param>
    /// <returns>A task representing the long-running accept loop.</returns>
    public Task StartAsync(CancellationToken ct) 
    {
        // drops the outer token binding to prevent aggressive TaskCanceledExceptions during rapid shutdown
        return Task.Run(() => AcceptLoop(ct));
    }

    /// <summary>
    /// Registers a pending streaming slot awaiting an incoming connection.
    /// </summary>
    /// <param name="slot">The slot state to register.</param>
    /// <returns>A unique routing token for the client handshake.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the slot is null.</exception>
    public string RegisterPendingStream(StreamingSlot slot)
    {
        if (slot == null) throw new ArgumentNullException(nameof(slot));
        
        var token = Guid.NewGuid().ToString();
        _pendingSessions[token] = slot;
        return token;
    }

    /// <summary>
    /// Continuously accepts incoming TCP connections securely.
    /// </summary>
    /// <param name="ct">The cancellation token to terminate the loop.</param>
    /// <returns>A task representing the active accept loop.</returns>
    private async Task AcceptLoop(CancellationToken ct)
    {
        try
        {
            _listener.Start();
            
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(ct);
                    
                    // fire and forget the client handler to maintain high throughput
                    _ = HandleStreamClientSafe(client, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // swallow transient socket errors (e.g. half-open connections)
                }
            }
        }
        finally
        {
            // guarantees the socket drops and the port is freed, preventing test/restart deadlocks
            _listener.Stop();
        }
    }

    /// <summary>
    /// Authenticates and routes an incoming client connection safely with timeouts and deterministic resource cleanup.
    /// </summary>
    /// <param name="client">The connected TCP client.</param>
    /// <param name="ct">The server cancellation token.</param>
    /// <returns>A task representing the stream handling lifecycle.</returns>
    private async Task HandleStreamClientSafe(TcpClient client, CancellationToken ct)
    {
        using var _ = client;

        try
        {
            await using var sslStream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
            
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
            
            await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = _certificate,
                ClientCertificateRequired = false,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, timeoutCts.Token);
            
            var (token, t0) = await ReadHead(sslStream, timeoutCts.Token);
            
            if (_pendingSessions.TryRemove(token, out var slot))
            {
                var inferenceEngine = _inferenceFactory.Create(t0);
                var receiver = new StreamingReceiver(sslStream, inferenceEngine, slot.Repository);
                
                var remoteIpEndPoint = client.Client.RemoteEndPoint as IPEndPoint 
                    ?? throw new InvalidOperationException("No available remote endpoint.");
                
                if (slot.TryActivate(remoteIpEndPoint.Address.ToString()))
                {
                    try 
                    { 
                        await receiver.RunAsync(ct); 
                    }
                    finally 
                    { 
                        slot.Deactivate(); 
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // ignore deliberate timeout or shutdown
        }
        catch (Exception)
        {
            // ignore TLS negotiation failures or abrupt socket closures
        }
    }

    /// <summary>
    /// Reads the protocol header from the negotiated stream.
    /// </summary>
    /// <param name="stream">The authenticated data stream.</param>
    /// <param name="ct">The token linking server lifecycle and connection timeout.</param>
    /// <returns>A tuple containing the routing token and the temporal zero-index.</returns>
    private static async Task<(string token, StreamTime t0)> ReadHead(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[32];
        await stream.ReadExactlyAsync(buffer, ct);
        
        var streamHead = new StreamHeader(buffer);
        return (streamHead.Token.ToString(), streamHead.T0);
    }
}