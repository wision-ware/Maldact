using FluentAssertions;
using Maldact.Backend.Server.ControlProtocol.Commands;
using Maldact.Core.Server.Authentication;

namespace Maldact.Tests.BackendTests.ServerTests.ControlProtocolTests.CommandsTests;

/// <summary>
/// Verifies the identity token validation and context mutation logic.
/// </summary>
public class AuthenticateCommandTests
{
    [Fact]
    public async Task Execute_MissingToken_WritesErrorAndBypassesAuth()
    {
        // arrange
        var context = ControlProtocolTestFactory.CreateContext(isAuthenticated: false);
        var command = new AuthenticateCommand(context).Command;
        var (config, _, err) = ControlProtocolTestFactory.BuildConfig(command);

        // act (note: System.CommandLine passes string.Empty for missing required arguments if not caught by the parser natively)
        await config.InvokeAsync("authenticate \"\"");

        // assert
        err.GetCommandResponse().Message.Should().Contain("No auth token provided");
        context.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task Execute_InvalidToken_WritesFailureMessage()
    {
        // arrange
        var context = ControlProtocolTestFactory.CreateContext(isAuthenticated: false, authenticator: new RejectingAuthenticator());
        var command = new AuthenticateCommand(context).Command;
        var (config, _, err) = ControlProtocolTestFactory.BuildConfig(command);

        // act
        await config.InvokeAsync("authenticate bad-token");

        // assert
        err.GetCommandResponse().Message.Should().Contain("Authentication failed");
        context.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task Execute_ValidToken_MutatesContextState()
    {
        // arrange
        var context = ControlProtocolTestFactory.CreateContext(isAuthenticated: false, authenticator: new AcceptingAuthenticator());
        var command = new AuthenticateCommand(context).Command;
        var (config, outCap, _) = ControlProtocolTestFactory.BuildConfig(command);

        // act
        await config.InvokeAsync("authenticate valid-token");

        // assert
        outCap.GetCommandResponse().Message.Should().Be(string.Empty, "success returns an empty space to format correctly.");
        context.IsAuthenticated.Should().BeTrue();
    }

    private class RejectingAuthenticator : IAuthenticator { public bool Authenticate(AuthToken token, out AccessPass? pass) { pass = null; return false; } }
    private class AcceptingAuthenticator : IAuthenticator { public bool Authenticate(AuthToken token, out AccessPass? pass) { pass = new AccessPass(AccessPass.Role.User); return true; } }
}