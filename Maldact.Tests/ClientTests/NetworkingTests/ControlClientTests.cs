using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FluentAssertions;
using Maldact.Client.Networking;
using Maldact.Client.Networking.ServerCommands;

namespace Maldact.Tests.ClientTests.NetworkingTests;

/// <summary>
/// Verifies the TLS connection lifecycle, persistent stream reading boundaries, and TCP error handling.
/// </summary>
public class ControlClientTests
{
   
    [Fact]
    public async Task ConnectAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // arrange - use a non-routable IP to force a guaranteed connection hang
        using var cts = new CancellationTokenSource();
        cts.Cancel(); 

        // act & assert
        await FluentActions.Invoking(() => ControlClient.ConnectAsync("192.0.2.1", 9999, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }
    
    [Fact]
    public async Task SendCommandAsync_SingleLineResponse_ReturnsParsedResponse()
    {
        // arrange
        await using var server = new EphemeralTlsServer();
        var clientTask = ControlClient.ConnectAsync("127.0.0.1", server.Port);
        await using var serverStream = await server.AcceptClientAsync();
        await using var client = await clientTask;

        // act
        var commandTask = client.SendCommandAsync(new ServerCommand("status"));
        
        // server simulates a fast, single-line atomic response
        await serverStream.WriteAsync(Encoding.UTF8.GetBytes("OK: Status is healthy\n"));
        var response = await commandTask;

        // assert
        response.Type.Should().Be(ResponseType.Ok);
        response.Message.Should().Be("OK: Status is healthy");
        response.Payload.Should().BeNull();
    }
    
    [Fact]
    public async Task SendCommandAsync_MultilinePayloadResponse_ReadsUntilPayloadEnd()
    {
        // arrange
        await using var server = new EphemeralTlsServer();
        var clientTask = ControlClient.ConnectAsync("127.0.0.1", server.Port);
        await using var serverStream = await server.AcceptClientAsync();
        await using var client = await clientTask;

        // act
        var commandTask = client.SendCommandAsync(new ServerCommand("results list"));
        
        // formats exact atomic payload structure the production backend emits
        var fullResponse = $"ERROR: Exception{Environment.NewLine}[PAYLOAD START]{Environment.NewLine}line 1{Environment.NewLine}line 2{Environment.NewLine}[PAYLOAD END]{Environment.NewLine}";
        await serverStream.WriteAsync(Encoding.UTF8.GetBytes(fullResponse));
        
        var response = await commandTask;

        // assert
        response.Type.Should().Be(ResponseType.Error);
        response.Payload.Should().Contain("line 1");
        response.Payload.Should().Contain("line 2");
    }
    
    /// <summary>
    /// A lightweight, disposable TLS server dedicated to safely testing client network loops without mock fragility.
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