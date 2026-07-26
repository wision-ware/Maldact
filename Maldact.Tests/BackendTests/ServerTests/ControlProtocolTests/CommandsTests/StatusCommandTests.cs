using System.Collections.Immutable;
using FluentAssertions;
using Maldact.Backend.Diagnostics;
using Maldact.Backend.Server.ControlProtocol.Commands;

namespace Maldact.Tests.BackendTests.ServerTests.ControlProtocolTests.CommandsTests;

/// <summary>
/// Verifies the secure retrieval and serialization of server telemetry.
/// </summary>
public class StatusCommandTests
{
    [Fact]
    public async Task Execute_Unauthenticated_ReturnsUnauthorized()
    {
        // arrange
        var context = ControlProtocolTestFactory.CreateContext(isAuthenticated: false);
        var command = new StatusCommand(context).Command;
        var (config, _, err) = ControlProtocolTestFactory.BuildConfig(command);

        // act
        await config.InvokeAsync("status");

        // assert
        err.GetCommandResponse().Message.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task Execute_Authenticated_SerializesMetricsPayload()
    {
        // arrange
        var metrics = new ServerDiagnosticMetrics(DateTimeOffset.UtcNow, 5, 0, 100, 1024, ImmutableArray<StreamDiagnostic>.Empty);
        var context = ControlProtocolTestFactory.CreateContext(isAuthenticated: true, metricsGetter: () => metrics);
        var command = new StatusCommand(context).Command;
        var (config, outCap, _) = ControlProtocolTestFactory.BuildConfig(command);

        // act
        await config.InvokeAsync("status");

        // assert
        var response = outCap.GetCommandResponse();
        response.Payload.Should().NotBeNull();
        response.Payload!.ToString().Should().Contain("\"activeStreamingSessions\":");
    }
}