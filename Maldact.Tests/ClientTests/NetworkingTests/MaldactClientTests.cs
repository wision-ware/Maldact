using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FluentAssertions;
using Maldact.Client.Indexing;
using Maldact.Client.Networking;
using Maldact.Core.Results;
using Maldact.Core.Server.Authentication;

namespace Maldact.Tests.ClientTests.NetworkingTests;

/// <summary>
/// Verifies the top-level client orchestration, authentication bounds, and stream duplication safeguards.
/// </summary>
public class MaldactClientTests
{
    /// <summary>
    /// Proves that when authentication fails, the client aggressively disposes the TCP socket to prevent port exhaustion.
    /// </summary>
    [Fact]
    public async Task ConnectAsync_AuthenticationRejected_ThrowsAndDisposesSocket()
    {
        // arrange
        await using var server = new EphemeralTlsServer();
        var stateIndex = new ConnectionStateIndex 
        { 
            Host = "127.0.0.1", 
            Port = server.Port, 
            AuthToken = new AuthToken("invalid-token") 
        };

        var serverTask = Task.Run(async () =>
        {
            await using var stream = await server.AcceptClientAsync();
            var buffer = new byte[1024];
            
            // 1. Read authenticate command
            await stream.ReadAsync(buffer);
            
            // 2. Reject it
            await stream.WriteAsync(Encoding.UTF8.GetBytes("ERROR: Unauthorized token\n"));
            
            // 3. Wait for client to dispose, read should immediately return 0 (EOF)
            return await stream.ReadAsync(buffer);
        });

        // act
        var ex = await Record.ExceptionAsync(() => MaldactClient.ConnectAsync(stateIndex));

        // assert
        ex.Should().BeOfType<AuthenticationException>();
        ex!.Message.Should().Contain("Couldn't authenticate");

        var finalRead = await serverTask;
        finalRead.Should().Be(0, "the client must proactively close and dispose the socket upon auth failure.");
    }

    /// <summary>
    /// Verifies that telemetry pushing is strictly locked until the secondary TLS pipeline is successfully allocated.
    /// </summary>
    [Fact]
    public async Task SendDataAsync_BeforeStreamChannelConnected_ThrowsInvalidOperationException()
    {
        // arrange
        await using var server = new EphemeralTlsServer();
        var stateIndex = new ConnectionStateIndex 
        { 
            Host = "127.0.0.1", 
            Port = server.Port, 
            AuthToken = new AuthToken("valid-token") 
        };

        var serverTask = Task.Run(async () =>
        {
            await using var stream = await server.AcceptClientAsync();
            var buffer = new byte[1024];
            
            await stream.ReadAsync(buffer);
            await stream.WriteAsync(Encoding.UTF8.GetBytes("OK: Authenticated\n"));
            
            // keep alive to maintain context
            await Task.Delay(1000); 
        });

        await using var client = await MaldactClient.ConnectAsync(stateIndex);

        // act
        var ex = await Record.ExceptionAsync(() => client.SendDataAsync([1.0f, 2.0f]));

        // assert
        ex.Should().BeOfType<InvalidOperationException>()
          .Which.Message.Should().Contain("No active streaming channel");
    }

    /// <summary>
    /// Proves that the client strictly prevents developers from allocating duplicate telemetry streams 
    /// over the same connection context, avoiding orphaned receiver sockets.
    /// </summary>
    [Fact]
    public async Task ConnectStreamChannelAsync_DuplicateCall_ThrowsInvalidOperationException()
    {
        // arrange
        await using var controlServer = new EphemeralTlsServer();
        await using var streamServer = new EphemeralTlsServer();

        var stateIndex = new ConnectionStateIndex 
        { 
            Host = "127.0.0.1", 
            Port = controlServer.Port, 
            AuthToken = new AuthToken("valid-token") 
        };

        var controlTask = Task.Run(async () =>
        {
            await using var stream = await controlServer.AcceptClientAsync();
            var buffer = new byte[1024];
            
            // 1. Auth success
            await stream.ReadAsync(buffer);
            await stream.WriteAsync(Encoding.UTF8.GetBytes("OK: Authenticated\n"));
            
            // 2. Stream allocation request
            await stream.ReadAsync(buffer);
            await stream.WriteAsync(Encoding.UTF8.GetBytes($"STREAM PORT {streamServer.Port} TOKEN {Guid.NewGuid()}\n"));
            
            // keep alive
            await Task.Delay(2000);
        });

        var streamTask = Task.Run(async () =>
        {
            await using var stream = await streamServer.AcceptClientAsync();
            var buffer = new byte[1024];
            // drain the 32-byte header handshake
            await stream.ReadAsync(buffer); 
            await Task.Delay(2000);
        });

        await using var client = await MaldactClient.ConnectAsync(stateIndex);
        
        // act - connect successfully
        await client.ConnectStreamChannelAsync(new StreamTime(TimeSpan.Zero));
        
        // act - attempt duplicate connection
        var ex = await Record.ExceptionAsync(() => client.ConnectStreamChannelAsync(new StreamTime(TimeSpan.Zero)));

        // assert
        ex.Should().BeOfType<InvalidOperationException>()
          .Which.Message.Should().Contain("already connected");
    }

    // --- SETUP HELPERS ---

    /// <summary>
    /// A lightweight, disposable TLS server to mock dynamic server responses.
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