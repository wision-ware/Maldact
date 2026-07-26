using System.Net.NetworkInformation;
using System.Security.Authentication;
using Maldact.Client.Indexing;
using Maldact.Client.Networking.ServerCommands;
using Maldact.Core.Results;
using Maldact.Core.Server.Authentication;
using Maldact.Core.Streaming;

namespace Maldact.Client.Networking;

/// <summary>
/// The primary facade for securely interacting with the Maldact server ecosystem, managing coordinated control and telemetry streams.
/// </summary>
public sealed class MaldactClient : IAsyncDisposable
{
    private readonly ControlClient _controlClient;
    private readonly AuthToken _authToken;
    
    private Streamer? _streamer;

    private MaldactClient(ControlClient controlClient, AuthToken token)
    {
        _controlClient = controlClient ?? throw new ArgumentNullException(nameof(controlClient));
        _authToken = token;
    }

    /// <summary>
    /// Establishes a control connection to the server and negotiates strict authentication.
    /// </summary>
    /// <param name="stateIndex">The connection routing and identity configuration.</param>
    /// <param name="ct">The token to cancel the connection or authentication process.</param>
    /// <returns>An authenticated and fully active client.</returns>
    /// <exception cref="AuthenticationException">Thrown if the server rejects the authorization key.</exception>
    public static async Task<MaldactClient> ConnectAsync(ConnectionStateIndex stateIndex, CancellationToken ct = default)
    {
        var control = await ControlClient.ConnectAsync(stateIndex.Host, stateIndex.Port, ct);
        
        try
        {
            var client = new MaldactClient(control, stateIndex.AuthToken);
            
            var result = await client.SendControlCommandAsync(new AuthenticateServerCommand(stateIndex.AuthToken), ct);
            
            if (result.Type == ResponseType.Ok) 
            {
                return client;
            }
            
            throw new AuthenticationException("Couldn't authenticate! Possibly incorrect or unauthorized key.");
        }
        catch
        {
            // prevents silent resource exhaustion by aggressively tearing down the TCP socket if auth fails
            await control.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Requests a telemetry stream allocation from the server and establishes the secondary high-throughput data pipeline.
    /// </summary>
    /// <param name="t0">The initial temporal reference point for the stream.</param>
    /// <param name="ct">The token to cancel the stream routing and handshake phase.</param>
    /// <exception cref="InvalidOperationException">Thrown if a stream is already active or routing fails.</exception>
    /// <exception cref="InvalidDataException">Thrown if the server yields an unparsable connection token.</exception>
    public async Task ConnectStreamChannelAsync(StreamTime t0, CancellationToken ct = default)
    {
        if (_streamer != null)
        {
            throw new InvalidOperationException("A streaming channel is already connected and active for this client.");
        }

        var streamResponse = await _controlClient.SendCommandAsync(new StreamServerCommand(), ct);
        
        if (streamResponse.Type == ResponseType.StreamReady && 
            streamResponse.Port.HasValue && 
            !string.IsNullOrWhiteSpace(streamResponse.ConnectionToken))
        {
            if (!Guid.TryParse(streamResponse.ConnectionToken, out var tokenGuid))
            {
                throw new InvalidDataException("The server provided a malformed or unrecognized connection token format.");
            }

            var header = new StreamHeader(tokenGuid, t0);
            
            _streamer = await Streamer.ConnectAsync(_controlClient.ConnectedHost, streamResponse.Port.Value, header, ct);
        }
        else
        {
            throw new InvalidOperationException($"Failed to establish stream routing. Server responded with: {streamResponse.Type} - {streamResponse.Message}");
        }
    }

    /// <summary>
    /// Dispatches a strongly-typed command to the server over the persistent control channel.
    /// </summary>
    /// <param name="command">The specific protocol command to execute.</param>
    /// <param name="ct">The cancellation token to abort the network operation.</param>
    /// <returns>The parsed server response.</returns>
    public async Task<ServerResponse> SendControlCommandAsync(ServerCommand command, CancellationToken ct = default)
    {
        return await _controlClient.SendCommandAsync(command, ct);
    }

    /// <summary>
    /// Pushes a high-throughput block of telemetry data to the server via the dedicated secondary stream.
    /// </summary>
    /// <param name="data">The floating-point inference payload.</param>
    /// <param name="ct">The cancellation token to abort the transmission.</param>
    public Task SendDataAsync(float[] data, CancellationToken ct = default)
    {
        if (_streamer is null) 
        {
            throw new InvalidOperationException(
                "Cannot send data: No active streaming channel connection. Call ConnectStreamChannelAsync first.");
        }
        
        return _streamer.SendAsync(data, ct);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _controlClient.DisposeAsync();
        
        if (_streamer != null) 
        {
            await _streamer.DisposeAsync();
        }
    }
}