using System.CommandLine;
using Maldact.Backend.Server.ControlProtocol.Responses;
using Maldact.Backend.Server.ControlProtocol.Utils;
using Maldact.Backend.Server.Streaming;
using Maldact.Core.Server;

namespace Maldact.Backend.Server.ControlProtocol.Commands;

/// <summary>
/// Handles the allocation and routing setup for inbound telemetry streams.
/// </summary>
internal class StreamCommand : CommandWrapper
{
    /// <summary>
    /// Gets the root command node for stream setup.
    /// </summary>
    public sealed override Command Command { get; protected set; }

    /// <summary>
    /// Initializes a new instance of the stream command.
    /// </summary>
    /// <param name="context">The execution context for the current session.</param>
    public StreamCommand(SessionContext context) : base(context)
    {
        Command = new Command("stream", "Setup streaming allocation from client.");
        Command.SetAction(StreamHandler);
    }

    private async Task StreamHandler(ParseResult parseResult)
    {
        try
        {
            // elevate the stream state for the client parser
            if (parseResult.Configuration.Output is CommandResultCapture capture)
            {
                capture.ResultType = CommandResponse.Type.Stream;
            }
            else
            {
                throw new IOException("Unsupported command output capture format.");
            }

            var pass = Context.AccessPass ?? throw new UnauthorizedAccessException();
            
            if (!Context.TryBookSlot(pass, out var slot) || slot == null) 
            {
                throw new InvalidOperationException("Cannot access streaming slot. It is likely already active or unavailable.");
            }

            var token = Context.StreamingManager.RegisterPendingStream(slot);

            await parseResult.Configuration.Output.WriteLineAsync(
                $"PORT {Context.StreamingManager.StreamingPort} TOKEN {token}");
        }
        catch (UnauthorizedAccessException)
        {
            await parseResult.Configuration.Error.WriteLineAsync(UnauthorizedAccessMessage);
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is IOException)
        {
            await parseResult.Configuration.Error.WriteLineAsync(ex.Message);
        }
    }
}