using System.CommandLine;
using Maldact.Core.Server;
using Maldact.Core.Server.Authentication;

namespace Maldact.Backend.Server.ControlProtocol.Commands;

/// <summary>
/// Handles global server termination requests requiring administrative privileges.
/// </summary>
internal class StopCommand : CommandWrapper
{
    /// <summary>
    /// Gets the root command node for server shutdown.
    /// </summary>
    public sealed override Command Command { get; protected set; }

    /// <summary>
    /// Initializes a new instance of the stop command.
    /// </summary>
    /// <param name="context">The execution context for the current session.</param>
    public StopCommand(SessionContext context) : base(context)
    {
        Command = new Command("stop", "Terminate the server runtime gracefully.");
        Command.SetAction(StopHandler);
    }

    private async Task StopHandler(ParseResult parseResult)
    {
        try
        {
            _ = Context.AccessPass ?? throw new UnauthorizedAccessException();
            
            Context.RequestServerShutdown();
            await parseResult.Configuration.Output.WriteLineAsync("Server shutdown requested.");
        }
        catch (UnauthorizedAccessException)
        {
            // catches both unauthenticated users AND non-admin authenticated users
            await parseResult.Configuration.Error.WriteLineAsync(UnauthorizedAccessMessage);
        }
    }
}