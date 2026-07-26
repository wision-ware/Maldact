using FluentAssertions;
using Maldact.Backend.Server.ControlProtocol.Responses;

namespace Maldact.Tests.BackendTests.ServerTests.ControlProtocolTests.ResponsesTests;

/// <summary>
/// Verifies the strict proprietary text framing format required for client network parsing.
/// </summary>
public class CommandResponseTests
{
    /// <summary>
    /// Ensures that responses without payloads format compactly without injecting protocol framing boundaries.
    /// </summary>
    [Fact]
    public void ToString_NoPayload_FormatsBasicMessage()
    {
        // arrange
        var response = new CommandResponse(CommandResponse.Type.Ok, "Success");

        // act
        var serialized = response.ToString();

        // assert
        serialized.Should().Be($"OK: Success\n");
        serialized.Should().NotContain("[PAYLOAD START]");
    }

    /// <summary>
    /// Proves that multiline payloads are strictly framed with the required protocol tags.
    /// </summary>
    [Fact]
    public void ToString_WithPayload_InjectsFramingBoundaries()
    {
        // arrange
        var response = new CommandResponse(CommandResponse.Type.Error, "Fatal Fault", "Stack Trace Data...");

        // act
        var serialized = response.ToString();

        // assert
        var expected = $"ERROR: Fatal Fault\n[PAYLOAD START]\nStack Trace Data...\n[PAYLOAD END]";
        serialized.Should().Be(expected);
    }

    /// <summary>
    /// Verifies that empty message fields correctly omit the colon separator.
    /// </summary>
    [Fact]
    public void ToString_EmptyMessage_OmitsColon()
    {
        // arrange
        var response = new CommandResponse(CommandResponse.Type.Stream, string.Empty);

        // act
        var serialized = response.ToString();

        // assert
        serialized.Should().Be($"STREAM\n");
    }
}