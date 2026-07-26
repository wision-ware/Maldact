using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Maldact.Client.Networking;
using Maldact.Core.Results;
using Maldact.Core.Streaming;

namespace Maldact.Tests.ClientTests.NetworkingTests;

/// <summary>
/// Verifies the secure telemetry transmission pipeline, pooling mechanics, and protocol handshakes.
/// </summary>
public class StreamerTests
{
    
    [Fact]
    public async Task ConnectAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // arrange - non-routable IP forces a connection stall
        var header = new StreamHeader(Guid.NewGuid(), new StreamTime(TimeSpan.Zero));
        using var cts = new CancellationTokenSource();
        cts.Cancel(); 

        // act & assert
        await FluentActions.Invoking(() => Streamer.ConnectAsync("192.0.2.1", 9999, header, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }
    
    [Fact]
    public async Task ConnectAsync_WritesValidStreamHeader()
    {
        // arrange
        await using var server = new EphemeralTlsServer();
        var expectedToken = Guid.NewGuid();
        var expectedTime = new StreamTime(TimeSpan.FromSeconds(10));
        var header = new StreamHeader(expectedToken, expectedTime);

        // act
        var clientTask = Streamer.ConnectAsync("127.0.0.1", server.Port, header);
        await using var serverStream = await server.AcceptClientAsync();
        await using var streamer = await clientTask;

        // assert - server reads the initial block
        var buffer = new byte[32];
        await serverStream.ReadExactlyAsync(buffer);
        
        var decodedHeader = new StreamHeader(buffer);
        decodedHeader.Token.Should().Be(expectedToken);
        decodedHeader.T0.AsRelative().Should().Be(expectedTime.AsRelative());
    }
    
    [Fact]
    public async Task SendAsync_TransmitsFloatDataCorrectly()
    {
        // arrange
        await using var server = new EphemeralTlsServer();
        var header = new StreamHeader(Guid.NewGuid(), new StreamTime(TimeSpan.Zero));
        
        var clientTask = Streamer.ConnectAsync("127.0.0.1", server.Port, header);
        await using var serverStream = await server.AcceptClientAsync();
        await using var streamer = await clientTask;

        // drain the initial 32-byte header so we are perfectly aligned for the payload
        var dumpBuffer = new byte[32];
        await serverStream.ReadExactlyAsync(dumpBuffer);

        float[] payload = { 1.0f, -3.14f, 42.0f, 0.0f };
        int expectedBytes = payload.Length * sizeof(float);

        // act
        await streamer.SendAsync(payload);

        // assert
        var receiveBuffer = new byte[expectedBytes];
        await serverStream.ReadExactlyAsync(receiveBuffer);

        // decode bytes back into floats to verify zero data loss
        var receivedFloats = new float[payload.Length];
        Buffer.BlockCopy(receiveBuffer, 0, receivedFloats, 0, expectedBytes);

        receivedFloats.Should().BeEquivalentTo(payload, "the memory span transmission should perfectly preserve the floating point structure.");
    }
    
    /// <summary>
    /// A lightweight, disposable TLS server to reliably test TCP telemetry boundaries without mock constraints.
    /// </summary>
    private class EphemeralTlsServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly X509Certificate2 _cert;
        public int Port { get; }

        public EphemeralTlsServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest("CN=MaldactTest", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            _cert = new X509Certificate2(req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)).Export(X509ContentType.Pfx));
        }

        public async Task<SslStream> AcceptClientAsync()
        {
            var tcpClient = await _listener.AcceptTcpClientAsync();
            var sslStream = new SslStream(tcpClient.GetStream(), false);
            
            await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = _cert,
                ClientCertificateRequired = false,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
            });
            
            return sslStream;
        }

        public ValueTask DisposeAsync()
        {
            _listener.Stop();
            _cert.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}