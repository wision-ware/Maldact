using FluentAssertions;
using Maldact.Backend.ML.Packaging;
using Maldact.CLI.Commands.Server;
using Maldact.Core.Config.ConfigDefinitions;

namespace Maldact.Tests.IntegrationTests;

[Collection("Integration")]
public sealed class ServerLifecycleIntegrationTests : IntegrationTestBase
{
    private readonly ServerConfiguration _sampleServerConfig;
    private new readonly ModelSpecification _sampleModelSpec;

    public ServerLifecycleIntegrationTests()
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

    /// <summary>
    /// Validates the server startup sequence, background task allocation, and deterministic cancellation teardown.
    /// </summary>
    [Fact]
    public async Task ServerStartCommand_BootSequence_StartsAndStopsGracefully()
    {
        // provision index requirements
        string serverConfigPath = WriteConfigFile("serverConfig.json", _sampleServerConfig);
        await App.RunAsync(new[] { "config", "set", "server-configuration", serverConfigPath });
        
        ServerStartCommand.ArtifactOverride = new DeploymentArtifact
        {
            Specification = _sampleModelSpec,
            Contract = new PreprocessingContract(),
            Parameters = null!, 
            ConsolidatorConfiguration = null!
        };

        using var cts = new CancellationTokenSource();
        
        // schedules an automatic cancellation signal to trip the interactive CLI loop
        cts.CancelAfter(TimeSpan.FromMilliseconds(800));

        try
        {
            this.Error.Input.PushKey(ConsoleKey.Y);
            this.Error.Input.PushKey(ConsoleKey.Enter);
            
            int exitCode = await App.RunAsync(new[] { "server", "start", "0", "0", "mock-artifact.zip" }, cts.Token);

            // verify successful shutdown resolution
            exitCode.Should().Be(0, "server should return a successful exit code upon graceful shutdown");

            // cross-verify dual-channel routing allocations
            this.Out.Output.Should().Contain("Server running", "stdout should emit live data streams");
            this.Out.Output.Should().Contain("Server stopped safely.");
            
            this.Error.Output.Should().Contain("Binding TCP listeners...", "stderr should absorb progress spinners");
            this.Error.Output.Should().Contain("Stopping background services...");
        }
        finally
        {
            // purge global state to maintain xUnit pipeline isolation
            ServerStartCommand.ArtifactOverride = null;
        }
    }
}