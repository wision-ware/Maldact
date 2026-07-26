using FluentAssertions;
using Maldact.Client.Indexing;
using Maldact.Core.Server.Authentication;

namespace Maldact.Tests.IntegrationTests;

[Collection("Integration")]
public sealed class ConnectionAndResultsIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task Run_DisconnectCommand_PurgesActiveConnectionStateIndexFile()
    {
        // Arrange - Seed the active connection index with a mock routing endpoint
        var activeConnection = new ConnectionStateIndex
        {
            Host = "127.0.0.1",
            Port = 5000,
            AuthToken = new AuthToken("secret-admin-token")
        };
        GlobalConnectionStateIndexManager.SetNewState(activeConnection);

        // Act
        int exitCode = await App.RunAsync(new[] { "disconnect" });

        // Assert
        exitCode.Should().Be(0);
        GlobalConnectionStateIndexManager.GetCurrentState().Should().BeNull("The active connection state must be fully cleared on disconnect commands.");
    }

    [Fact]
    public async Task ResultsCommand_WithNoActiveConnectionState_InterceptsFailureCleanly()
    {
        // Arrange - Ensure no server routing indices are saved on disk
        GlobalConnectionStateIndexManager.SetNewState(null);

        // Act
        int exitCode = await App.RunAsync(new[] { "results", "latest" });

        // Assert
        exitCode.Should().Be(-1);
    }
}