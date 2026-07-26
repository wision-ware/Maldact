using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Maldact.Client.Networking.ServerCommands;

namespace Maldact.Client.Networking;

/// <summary>
/// Provides a persistent, secure connection to the control server for command execution.
/// </summary>
internal sealed class ControlClient : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly SslStream _stream;
    private readonly StreamReader _reader;
    
    /// <summary>
    /// Gets the hostname of the actively connected server.
    /// </summary>
    public string ConnectedHost { get; }

    private ControlClient(TcpClient client, SslStream stream, string host)
    {
        _client = client;
        _stream = stream;
        ConnectedHost = host;
        
        _reader = new StreamReader(_stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
    }

    /// <summary>
    /// Establishes a new secure TLS session with the target control server.
    /// </summary>
    /// <param name="host">The remote server address.</param>
    /// <param name="port">The remote server port.</param>
    /// <param name="ct">The cancellation token to abort the connection attempt.</param>
    /// <returns>An initialized and authenticated control client.</returns>
    public static async Task<ControlClient> ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        var client = new TcpClient();
        
        try
        {
            await client.ConnectAsync(host, port, ct);
            var sslStream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false, TrustAllCertsCallback, null);
            
            await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "MaldactServer",
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
            }, ct);
            
            return new ControlClient(client, sslStream, host);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Transmits a control command and intelligently reads the dynamically framed server response.
    /// </summary>
    /// <param name="command">The command payload to execute.</param>
    /// <param name="ct">The cancellation token to abort the operation.</param>
    /// <returns>The parsed server response.</returns>
    /// <exception cref="EndOfStreamException">Thrown if the server severs the connection during execution.</exception>
    public async Task<ServerResponse> SendCommandAsync(ServerCommand command, CancellationToken ct = default)
    {
        var bytes = command.GetBytes();
        await _stream.WriteAsync(bytes.AsMemory(), ct);
        
        var responseTextBuilder = new StringBuilder();
        var buffer = new char[4096];
        
        // read the first block
        int charsRead = await _reader.ReadAsync(buffer, ct);
        if (charsRead == 0) throw new EndOfStreamException("The server unexpectedly closed the control stream.");
        
        responseTextBuilder.Append(buffer, 0, charsRead);
        
        // dynamically evaluate if a payload stream was initiated
        if (responseTextBuilder.ToString().Contains("[PAYLOAD START]"))
        {
            while (!responseTextBuilder.ToString().Contains("[PAYLOAD END]"))
            {
                charsRead = await _reader.ReadAsync(buffer, ct);
                if (charsRead == 0) throw new EndOfStreamException("The server unexpectedly closed the control stream.");
                responseTextBuilder.Append(buffer, 0, charsRead);
            }
        }
        else
        {
            // if it's a single-line response, ensure we read until the line break
            while (!responseTextBuilder.ToString().EndsWith("\n"))
            {
                charsRead = await _reader.ReadAsync(buffer, ct);
                if (charsRead == 0) throw new EndOfStreamException("The server unexpectedly closed the control stream.");
                responseTextBuilder.Append(buffer, 0, charsRead);
            }
        }
        
        return ServerResponse.Parse(responseTextBuilder.ToString());
    }
    
    // todo: replace with certificate pinning or OS trust store validation before production deployment
    private static bool TrustAllCertsCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) 
        => true;
    
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _reader.Dispose();
        await _stream.DisposeAsync();
        _client.Dispose();
    }
}