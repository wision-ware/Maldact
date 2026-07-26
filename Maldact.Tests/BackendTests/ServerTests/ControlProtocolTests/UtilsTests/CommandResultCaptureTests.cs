using FluentAssertions;
using Maldact.Backend.Server.ControlProtocol.Responses;
using Maldact.Backend.Server.ControlProtocol.Utils;

namespace Maldact.Tests.BackendTests.ServerTests.ControlProtocolTests.UtilsTests;

/// <summary>
/// Verifies the string parsing, memory allocation boundaries, and payload framing of command executions.
/// </summary>
public class CommandResultCaptureTests
{
    
    [Fact]
    public void GetCommandResponse_EmptyBuffer_ThrowsInvalidOperationException()
    {
        // arrange
        using var capture = new CommandResultCapture();

        // act & assert
        FluentActions.Invoking(() => capture.GetCommandResponse())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*No output to parse*");
    }
    
    [Fact]
    public void GetCommandResponse_SingleLineOutput_MapsToMessageOnly()
    {
        // arrange
        using var capture = new CommandResultCapture { ResultType = CommandResponse.Type.Ok };
        capture.Write("Process completed successfully.");

        // act
        var response = capture.GetCommandResponse();

        // assert
        response.ResponseType.Should().Be(CommandResponse.Type.Ok);
        response.Message.Should().Be("Process completed successfully.");
        response.Payload.Should().BeNull();
    }
    
    [Fact]
    public void GetCommandResponse_MultilineOutput_SplitsMessageAndPayload()
    {
        // arrange
        using var capture = new CommandResultCapture();
        capture.WriteLine("Data fetched");
        capture.WriteLine("Item 1");
        capture.Write("Item 2");

        // act
        var response = capture.GetCommandResponse();

        // assert
        response.Message.Should().Be("Data fetched");
        response.Payload.Should().Be($"Item 1{Environment.NewLine}Item 2");
    }
    
    [Fact]
    public void Clear_ResetsInternalBuffer()
    {
        // arrange
        using var capture = new CommandResultCapture();
        capture.Write("Stale data");

        // act
        capture.Clear();

        // assert
        FluentActions.Invoking(() => capture.GetCommandResponse())
            .Should().Throw<InvalidOperationException>("because the buffer was wiped clean.");
    }
}