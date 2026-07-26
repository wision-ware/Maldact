using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Maldact.Backend.ML.Packaging;
using Maldact.CLI.Commands.Server;
using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Maldact.Client.Networking;
using Maldact.Client.Networking.ServerCommands;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.Server.Authentication;

namespace Maldact.Tests.IntegrationTests;

[Collection("Integration")]
public sealed class ServerRobustnessIntegrationTests : IntegrationTestBase
{
    private readonly ServerConfiguration _sampleServerConfig;
    private new readonly ModelSpecification _sampleModelSpec;

    public ServerRobustnessIntegrationTests()
    {
        _sampleServerConfig = new ServerConfiguration
        {
            ServerId = "test-node-01",
            AdminKeys = new() { "admin-token-xyz" },
            UserKeys = new() { "user-token-abc" }
        };

        _sampleModelSpec = new ModelSpecification
        {
            Name = "Integration_CNN_v1",
            Algorithm = ModelSpecification.AlgorithmType.Cnn,
            InputDimension = 2,
            OutputDimension = 1,
            WindowSize = 10,
            WindowStride = 2,
            Cnn = new CnnParameters { ChannelSizes = new[] { 16 }, KernelSize = 3, Stride = 1 }
        };
    }

    [Fact]
    public async Task Server_WhenSubjectedToMalformedCommandsAndDroppedSockets_SurvivesAndProcessesValidCommands()
    {
        string serverConfigPath = WriteConfigFile("serverConfig.json", _sampleServerConfig);
        await App.RunAsync(new[] { "config", "set", "server-configuration", serverConfigPath });

        ServerStartCommand.ArtifactOverride = new DeploymentArtifact
        {
            Specification = _sampleModelSpec,
            Contract = new PreprocessingContract(),
            Parameters = null!, 
            ConsolidatorConfiguration = null!
        };

        using var serverCts = new CancellationTokenSource();
        
        // boot the server in the background
        var serverTask = Task.Run(async () =>
        {
            await App.RunAsync(new[] { "server", "start", "5700", "5701", "mock.zip" }, serverCts.Token);
        });

        // allow the TCP listeners to fully bind before attacking
        bool isServerUp = false;
        for (int i = 0; i < 30; i++)
        {
            try
            {
                using var pingClient = new TcpClient();
                await pingClient.ConnectAsync("127.0.0.1", 5701);
                isServerUp = true;
                break; // The server is up and listening!
            }
            catch (SocketException)
            {
                await Task.Delay(500); // Sleep and try again
            }
        }

        if (!isServerUp)
        {
            throw new TimeoutException("The Maldact server failed to bind port 5601 within the CI timeout period.");
        }

        try
        {
            // Attack 1: Connect, complete TLS, send garbage data, and drop socket
            await ExecuteRawTcpAttackAsync(5700, "MALFORMED_GARBAGE_COMMAND: SELECT * FROM Users;");

            // Attack 2: Connect, complete TLS, send a valid command without authentication, and drop socket
            await ExecuteRawTcpAttackAsync(5700, "STREAM");

            // Attack 3: Abrupt TCP connection drop before TLS handshake even completes
            using (var rawClient = new TcpClient())
            {
                await rawClient.ConnectAsync("127.0.0.1", 5700);
                rawClient.Close(); 
            }
            
            var connectionState = new ConnectionStateIndex
            {
                Host = "127.0.0.1",
                Port = 5700,
                AuthToken = new AuthToken("admin-token-xyz")
            };
            
            // todo
            // ControlClient.ValidatorOverride = (sender, cert, chain, errors) => true;

            // Ping the server directly through the native network layer
            await using var maldactClient = await MaldactClient.ConnectAsync(connectionState);
            var response = await maldactClient.SendControlCommandAsync(new StatusServerCommand());

            // Assert the server survived the attack and successfully generated a telemetry payload
            response.Should().NotBeNull();
            response.Payload.Should().Contain("activeStreamingSessions", "the server must return a valid JSON telemetry payload after surviving an attack");
        }
        finally
        {
            this.Error.Input.PushKey(ConsoleKey.Y);
            this.Error.Input.PushKey(ConsoleKey.Enter);
            
            await serverCts.CancelAsync();
            await serverTask;
            
            ServerStartCommand.ArtifactOverride = null;
            // ControlClient.ValidatorOverride = null;
            
            Terminal.Out = this.Out;
            Spectre.Console.AnsiConsole.Console = this.Out;
        }
    }

    private static async Task ExecuteRawTcpAttackAsync(int port, string payload)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port);
        
        using var rawStream = client.GetStream();
        using var sslStream = new SslStream(rawStream, false, (sender, cert, chain, errors) => true, null);
        
        await sslStream.AuthenticateAsClientAsync("127.0.0.1");
        
        byte[] bytes = Encoding.UTF8.GetBytes(payload + Environment.NewLine);
        await sslStream.WriteAsync(bytes);
        
        // forcefully abort the socket immediately after writing, simulating a network crash
        client.Client.Close(0); 
    }
    
    [Fact]
    public async Task Server_WhenStreamingChannelSubjectedToAttacks_SurvivesAndProcessesValidCommands()
    {
        string serverConfigPath = WriteConfigFile("serverConfig.json", _sampleServerConfig);
        await App.RunAsync(new[] { "config", "set", "server-configuration", serverConfigPath });

        ServerStartCommand.ArtifactOverride = new DeploymentArtifact
        {
            Specification = _sampleModelSpec,
            Contract = new PreprocessingContract(),
            Parameters = null!, 
            ConsolidatorConfiguration = null!
        };

        using var serverCts = new CancellationTokenSource();
        
        var serverTask = Task.Run(async () =>
        {
            await App.RunAsync(new[] { "server", "start", "5600", "5601", "mock-artifact.zip" }, serverCts.Token);
        });

        bool isServerUp = false;
        for (int i = 0; i < 30; i++)
        {
            try
            {
                using var pingClient = new TcpClient();
                await pingClient.ConnectAsync("127.0.0.1", 5601);
                isServerUp = true;
                break; // The server is up and listening!
            }
            catch (SocketException)
            {
                await Task.Delay(500); // Sleep and try again
            }
        }

        if (!isServerUp)
        {
            throw new TimeoutException("The Maldact server failed to bind port 5601 within the CI timeout period.");
        }

        try
        {
            // Attack 1: Connect to the streaming port and immediately hang up (TCP FIN/RST)
            using (var rawClient = new TcpClient())
            {
                await rawClient.ConnectAsync("127.0.0.1", 5601);
                rawClient.Close(); 
            }

            // Attack 2: Connect, send a massive barrage of unformatted garbage bytes, and drop
            using (var rawClient = new TcpClient())
            {
                await rawClient.ConnectAsync("127.0.0.1", 5601);
                using var rawStream = rawClient.GetStream();
                
                // If the streaming port expects TLS, this will crash the handshake.
                // If it expects a specific framing header, this will crash the protocol parser.
                // Either way, the server must swallow it!
                byte[] garbage = Encoding.UTF8.GetBytes("UNDEFINED_SLOT_ID: I AM A HACKER DROPPING MALFORMED DATA IN YOUR STREAM!!!");
                await rawStream.WriteAsync(garbage.AsMemory());
                
                rawClient.Client.Close(0); // Forceful abort
            }

            // Give the streaming manager 100ms to clean up the dead sockets
            await Task.Delay(100);

            // Verify Survival: The control channel should still be perfectly responsive
            var connectionState = new ConnectionStateIndex
            {
                Host = "127.0.0.1",
                Port = 5600,
                AuthToken = new AuthToken("admin-token-xyz")
            };
            
            // todo
            // ControlClient.ValidatorOverride = (sender, cert, chain, errors) => true;

            await using var maldactClient = await MaldactClient.ConnectAsync(connectionState, default);
            var response = await maldactClient.SendControlCommandAsync(new StatusServerCommand(), default);

            response.Should().NotBeNull();
            response.Payload.Should().Contain("activeStreamingSessions", "the server must survive streaming channel attacks and continue serving control payloads");
        }
        finally
        {
            // Feed the ghost keystrokes for a clean UI teardown
            this.Error.Input.PushKey(ConsoleKey.Y);
            this.Error.Input.PushKey(ConsoleKey.Enter);

            await serverCts.CancelAsync();
            await serverTask;
            
            ServerStartCommand.ArtifactOverride = null;
            // ControlClient.ValidatorOverride = null;
        }
    }
}