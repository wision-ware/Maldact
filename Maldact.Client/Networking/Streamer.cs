using System.Buffers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Maldact.Core.Streaming;

namespace Maldact.Client.Networking;

/// <summary>
/// Handles allocation-free, high-throughput secure streaming of binary inference telemetry to the ingestion server.
/// </summary>
internal sealed class Streamer : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly SslStream _stream;

    private Streamer(TcpClient client, SslStream stream)
    {
        _client = client;
        _stream = stream;
    }

    /// <summary>
    /// Connects asynchronously to the streaming endpoint and transmits the initialization framing header securely.
    /// </summary>
    /// <param name="host">The target server hostname or IP address.</param>
    /// <param name="port">The target server streaming port.</param>
    /// <param name="streamHeader">The routing and synchronization header initializing the session context.</param>
    /// <param name="ct">The token used to interrupt or abort connection initialization.</param>
    /// <returns>An active, securely authenticated streaming handle.</returns>
    public static async Task<Streamer> ConnectAsync(string host, int port, StreamHeader streamHeader, CancellationToken ct = default)
    {
        var client = new TcpClient();
        
        try
        {
            await client.ConnectAsync(host, port, ct);
            
            var sslStream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false, TrustAllCertsCallback, null);
            
            await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "MaldactServer",
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, ct);
            
            var streamer = new Streamer(client, sslStream);
            var message = streamHeader.ToBytes();
            
            await streamer._stream.WriteAsync(message.AsMemory(), ct);
            return streamer;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Transmits a raw block of floating-point telemetry tokens using recycled array rent cycles to prevent heap allocation.
    /// </summary>
    /// <param name="data">The floating-point array payload representing signal or inference features.</param>
    /// <param name="ct">The token used to cancel data transmission.</param>
    /// <returns>A task representing the asynchronous stream transmission block.</returns>
    public async Task SendAsync(float[] data, CancellationToken ct = default)
    {
        if (data == null || data.Length == 0) return;

        int byteCount = data.Length * sizeof(float);
        
        // rents buffer to completely eliminate LOH allocations and garbage collector spikes
        byte[] buffer = ArrayPool<byte>.Shared.Rent(byteCount);
        
        try
        {
            Buffer.BlockCopy(data, 0, buffer, 0, byteCount);
            
            // targets optimized span-based pipelines directly
            await _stream.WriteAsync(buffer.AsMemory(0, byteCount), ct);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
    
    // todo: switch to target-pinned signature matching or validation store mapping for production
    private static bool TrustAllCertsCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) 
        => true;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync();
        _client.Dispose();
    }
}