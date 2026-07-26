using FluentAssertions;
using Maldact.Backend.Server;
using Maldact.Backend.Server.ControlProtocol.Commands;
using Maldact.Backend.Server.ControlProtocol.Responses;
using Maldact.Backend.Server.Streaming;
using Maldact.Core.Results;
using Maldact.Core.Server.Authentication;

namespace Maldact.Tests.BackendTests.ServerTests.ControlProtocolTests.CommandsTests;

/// <summary>
/// Verifies the dynamic allocation of ingress network slots via the CLI.
/// </summary>
public class StreamCommandTests
{
    [Fact]
    public async Task Execute_SlotBookingFails_WritesErrorMessage()
    {
        // arrange
        SessionContext.SlotTryBooker rejectingBooker = (AccessPass p, out StreamingSlot? s) => { s = null; return false; };
        var context = ControlProtocolTestFactory.CreateContext(isAuthenticated: true, booker: rejectingBooker);
        
        var command = new StreamCommand(context).Command;
        var (config, _, err) = ControlProtocolTestFactory.BuildConfig(command);

        // act
        await config.InvokeAsync("stream");

        // assert
        err.GetCommandResponse().Message.Should().Contain("Cannot access streaming slot");
    }

    [Fact]
    public async Task Execute_ValidSlot_RegistersStreamAndOutputsConnectionInfo()
    {
        // arrange
        SessionContext.SlotTryBooker acceptingBooker = (AccessPass p, out StreamingSlot? s) => 
        { 
            s = new StreamingSlot(new StubRepository()); 
            return true; 
        };
        var context = ControlProtocolTestFactory.CreateContext(isAuthenticated: true, booker: acceptingBooker);
        
        var command = new StreamCommand(context).Command;
        var (config, outCap, _) = ControlProtocolTestFactory.BuildConfig(command);

        // act
        await config.InvokeAsync("stream");

        // assert
        var response = outCap.GetCommandResponse();
        response.ResponseType.Should().Be(CommandResponse.Type.Stream);
        response.Message.Should().Contain("PORT");
        response.Message.Should().Contain("TOKEN");
    }

    // stub strictly to satisfy slot dependencies
    private class StubRepository : IResultRepository 
    {
        public Task SaveAsync(ResultEntry[] results) => Task.CompletedTask;
        public Task<ResultEntry[]> GetAsync(string[] resultIds) => throw new NotImplementedException();
        public Task<ResultEntry?> GetLatestAsync() => throw new NotImplementedException();
        public Task DeleteAsync(string[] resultIds) => throw new NotImplementedException();
        public Task<IEnumerable<ResultEntry>> QueryAsync(ResultQuery query) => throw new NotImplementedException();
        public Task QueryDeleteAsync(ResultQuery query) => throw new NotImplementedException();
        public Task ClearAsync() => throw new NotImplementedException();
        public Task<int> GetCountAsync() => throw new NotImplementedException();
    }
}