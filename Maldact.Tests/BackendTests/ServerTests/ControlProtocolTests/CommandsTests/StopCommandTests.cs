using FluentAssertions;
using Maldact.Backend.Server.ControlProtocol.Commands;
using Maldact.Core.Server.Authentication;

namespace Maldact.Tests.BackendTests.ServerTests.ControlProtocolTests.CommandsTests;

/// <summary>
/// Verifies the administrative teardown boundaries.
/// </summary>
public class StopCommandTests
{
    [Fact]
    public async Task Execute_UserRole_ReturnsUnauthorized()
    {
        // arrange
        var context = ControlProtocolTestFactory.CreateContext(isAuthenticated: true, role: AccessPass.Role.User);
        var command = new StopCommand(context).Command;
        var (config, _, err) = ControlProtocolTestFactory.BuildConfig(command);

        // act
        await config.InvokeAsync("stop");
        await Task.Delay(600); // wait till shutdown request goes through

        // assert
        err.GetCommandResponse().Message.Should().Contain("Unauthorized");
        context.ServerCancellationToken.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task Execute_AdminRole_TripsServerCancellationToken()
    {
        // arrange
        var context = ControlProtocolTestFactory.CreateContext(isAuthenticated: true, role: AccessPass.Role.Admin);
        var command = new StopCommand(context).Command;
        var (config, outCap, _) = ControlProtocolTestFactory.BuildConfig(command);

        // act
        await config.InvokeAsync("stop");
        await Task.Delay(600); // wait till shutdown request goes through

        // assert
        outCap.GetCommandResponse().Message.Should().Contain("shutdown requested");
        context.ServerCancellationToken.IsCancellationRequested.Should().BeTrue("the admin context successfully bypassed security and fired the CTS.");
    }
}