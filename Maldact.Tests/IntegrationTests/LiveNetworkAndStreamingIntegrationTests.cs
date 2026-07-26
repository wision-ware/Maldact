using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FluentAssertions;
using Maldact.CLI.Commands.Stream;
using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Maldact.Client.Networking;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.Server.Authentication;

namespace Maldact.Tests.IntegrationTests;

[Collection("Integration")]
public sealed class LiveNetworkAndStreamingIntegrationTests : IntegrationTestBase
{
    /// <summary>
    /// Generates an ephemeral self-signed certificate bound to the loopback adapter.
    /// </summary>
    /// <returns>A valid X509 certificate for local TLS execution.</returns>
    private static X509Certificate2 CreateTemporaryTestCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=127.0.0.1", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return new X509Certificate2(cert.Export(X509ContentType.Pfx));
    }
    
    /// <summary>
    /// Custom stream wrapper that blocks indefinitely rather than returning EOF 0.
    /// Intercepts cancellation to yield a graceful 0-byte EOF, bypassing generic exception handlers.
    /// </summary>
    private sealed class InfiniteMockStream : MemoryStream
    {
        public InfiniteMockStream(byte[] buffer) : base(buffer) { }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position >= Length)
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return 0; 
                }
                
                return 0;
            }
            
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }

    [Fact]
    public async Task FullPipelineRun_NegotiatesStreamingSlotAndFetchesResult_Succeeds()
    {
        string modelSpecPath = WriteConfigFile("modelSpec.json", _sampleModelSpec);
        await App.RunAsync(new[] { "config", "set", "model-specification", modelSpecPath });

        using var serverCertificate = CreateTemporaryTestCertificate();
        using var streamCts = new CancellationTokenSource();
        
        var controlListener = new TcpListener(IPAddress.Loopback, 0);
        var streamingListener = new TcpListener(IPAddress.Loopback, 0);
        controlListener.Start();
        streamingListener.Start();
        
        int controlPort = ((IPEndPoint)controlListener.LocalEndpoint).Port;
        int streamingPort = ((IPEndPoint)streamingListener.LocalEndpoint).Port;
        Guid mockStreamToken = Guid.NewGuid();

        // bypass client auth via direct config mutation
        GlobalConnectionStateIndexManager.SetNewState(new ConnectionStateIndex
        {
            Host = "127.0.0.1",
            Port = controlPort,
            AuthToken = new AuthToken("admin-token-xyz")
        });

        // ControlClient.ValidatorOverride = (sender, certificate, chain, sslPolicyErrors) => true;

        var controlTask = Task.Run(async () =>
        {
            using (var socket = await controlListener.AcceptTcpClientAsync())
            using (var rawStream = socket.GetStream())
            using (var sslStream = new SslStream(rawStream, false))
            {
                await sslStream.AuthenticateAsServerAsync(serverCertificate);
                using var reader = new StreamReader(sslStream, Encoding.UTF8);
                using var writer = new StreamWriter(sslStream, Encoding.UTF8) { AutoFlush = true };
                _ = await reader.ReadLineAsync();
                await writer.WriteLineAsync("OK");
            }

            using (var socket = await controlListener.AcceptTcpClientAsync())
            using (var rawStream = socket.GetStream())
            using (var sslStream = new SslStream(rawStream, false))
            {
                await sslStream.AuthenticateAsServerAsync(serverCertificate);
                using var reader = new StreamReader(sslStream, Encoding.UTF8);
                using var writer = new StreamWriter(sslStream, Encoding.UTF8) { AutoFlush = true };
                _ = await reader.ReadLineAsync();
                await writer.WriteLineAsync("OK");
                _ = await reader.ReadLineAsync();
                await writer.WriteLineAsync($"STREAM: PORT {streamingPort} TOKEN {mockStreamToken}");
            }

            using (var socket = await controlListener.AcceptTcpClientAsync())
            using (var rawStream = socket.GetStream())
            using (var sslStream = new SslStream(rawStream, false))
            {
                await sslStream.AuthenticateAsServerAsync(serverCertificate);
                using var reader = new StreamReader(sslStream, Encoding.UTF8);
                using var writer = new StreamWriter(sslStream, Encoding.UTF8) { AutoFlush = true };
                _ = await reader.ReadLineAsync();
                await writer.WriteLineAsync("OK");
                _ = await reader.ReadLineAsync();
                string fullResponse = "OK\n[PAYLOAD START]\n[S1000 - E2500 | C1750] ID event_942 SCORE 0.987 RESULT anomaly\n[PAYLOAD END]";
                await writer.WriteLineAsync(fullResponse);
                await Task.WhenAny(reader.ReadToEndAsync(), Task.Delay(5000));
            }
        });

        var streamingTask = Task.Run(async () =>
        {
            using var socket = await streamingListener.AcceptTcpClientAsync();
            using var rawStream = socket.GetStream();
            using var sslStream = new SslStream(rawStream, false);
            await sslStream.AuthenticateAsServerAsync(serverCertificate);

            byte[] headerBuffer = new byte[32];
            _ = await sslStream.ReadAsync(headerBuffer, 0, 32);

            byte[] buffer = new byte[1024];
            _ = await sslStream.ReadAsync(buffer, 0, buffer.Length);

            await streamCts.CancelAsync();
        });

        try
        {
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            
            Terminal.Out = Out;
            Terminal.Error = Out;
            Spectre.Console.AnsiConsole.Console = Out;
            
            int connectResult = await App.RunAsync(new[] { "connect", "127.0.0.1", controlPort.ToString(), "admin-token-xyz" });
            connectResult.Should().Be(0);

            float[] simulatedTelemetry = new float[16384]; 
            simulatedTelemetry[0] = 1.2f;
            simulatedTelemetry[1] = 3.4f;
            simulatedTelemetry[2] = 5.6f;
            simulatedTelemetry[3] = 7.8f;
        
            byte[] rawBinaryBytes = new byte[simulatedTelemetry.Length * sizeof(float)];
            Buffer.BlockCopy(simulatedTelemetry, 0, rawBinaryBytes, 0, rawBinaryBytes.Length);
            
            using var mockMemoryPipe = new InfiniteMockStream(rawBinaryBytes);
            StreamCommand.GetStandardInputStream = () => mockMemoryPipe;

            try
            {
                int streamResult = await App.RunAsync(new[] { "stream" }, streamCts.Token);
                streamResult.Should().Be(0);
            }
            finally
            {
                StreamCommand.GetStandardInputStream = System.Console.OpenStandardInput;
            }

            int resultsResult = await App.RunAsync(new[] { "results", "latest" });
            resultsResult.Should().Be(0);

            Out.Output.Should().Contain("[S1000 - E2500 | C1750] ID event_942 SCORE 0.987 RESULT anomaly");
            
            await Task.WhenAll(controlTask, streamingTask);
        }
        finally
        {
            // ControlClient.ValidatorOverride = null;
            controlListener.Stop();
            streamingListener.Stop();
        }
    }
    

    /// <summary>
    /// Validates that explicit error packets from the remote host are properly intercepted, decoded, and printed by the client without throwing raw exceptions.
    /// </summary>
    [Fact]
    public async Task ResultsQuery_WhenServerReturnsErrorPayload_ReturnsExitCodeOneAndPrintsMessage()
    {
        using var serverCertificate = CreateTemporaryTestCertificate();
        var testServerListener = new TcpListener(IPAddress.Loopback, 0);
        testServerListener.Start();
        int assignedPort = ((IPEndPoint)testServerListener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var socket = await testServerListener.AcceptTcpClientAsync();
            using var rawStream = socket.GetStream();
            using var sslStream = new SslStream(rawStream, leaveInnerStreamOpen: false);
            
            await sslStream.AuthenticateAsServerAsync(serverCertificate);

            using var reader = new StreamReader(sslStream, Encoding.UTF8);
            using var writer = new StreamWriter(sslStream, Encoding.UTF8) { AutoFlush = true };

            // 1. read auth handshake request sent by client
            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("OK");
            
            // 2. read concrete command request ('ResultsLatestServerCommand')
            _ = await reader.ReadLineAsync();
            
            // 3. respond with explicit protocol error packet
            await writer.WriteLineAsync("ERROR: The requested evaluation event span has already been garbage-collected.");
        });

        try
        {
            var testConnection = new ConnectionStateIndex
            {
                Host = "127.0.0.1",
                Port = assignedPort,
                AuthToken = new AuthToken("admin-token-xyz")
            };
            GlobalConnectionStateIndexManager.SetNewState(testConnection);
            
            // override client tls chain validation to prevent ephemeral test cert rejection
            // ControlClient.ValidatorOverride = (sender, certificate, chain, sslPolicyErrors) => true;

            int exitCode = await App.RunAsync(new[] { "results", "latest" });

            exitCode.Should().Be(1, "the base command class should yield an execution error code when receiving error status packets");
            
            Error.Output.Should().Contain("Server Error: ERROR: The requested evaluation event span");
            
            await serverTask;
        }
        finally
        {
            // ControlClient.ValidatorOverride = null;
            testServerListener.Stop();
        }
    }
}