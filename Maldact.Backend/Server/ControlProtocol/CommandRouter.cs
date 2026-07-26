using System.CommandLine;
using System.Runtime.CompilerServices;
using System.Text;
using Maldact.Backend.Server.ControlProtocol.Commands;
using Maldact.Backend.Server.ControlProtocol.Responses;
using Maldact.Backend.Server.ControlProtocol.Utils;
using Maldact.Core.Server;

namespace Maldact.Backend.Server.ControlProtocol;

/// <summary>
/// Securely routes raw string input to the appropriate command handlers, strictly isolating parsing state and memory per session.
/// </summary>
internal class CommandRouter
{
    /// <summary>
    /// Encapsulates the cached command parser and a concurrency lock for a single session.
    /// </summary>
    private record SessionRouterState(
        CommandLineConfiguration CommandParser,
        SemaphoreSlim ExecutionLock);

    // seamlessly binds the configuration to the session's GC lifecycle, eliminating memory leaks and dictionary thread-safety issues
    private readonly ConditionalWeakTable<SessionContext, SessionRouterState> _sessionStates = new();

    /// <summary>
    /// Builds the root command tree and injects the session context into the leaf handlers.
    /// </summary>
    /// <param name="context">The isolated session executing the commands.</param>
    /// <returns>A fully configured command line parser.</returns>
    private static CommandLineConfiguration GetCommandLineConfiguration(SessionContext context)
    {
        var rootCommand = new RootCommand("Maldact Control Protocol")
        {
            new ResultsCommands(context).Command,
            new StatusCommand(context).Command,
            new StopCommand(context).Command,
            new AuthenticateCommand(context).Command,
            new StreamCommand(context).Command
        };
        
        return new CommandLineConfiguration(rootCommand);
    }
    
    /// <summary>
    /// Executes a raw command string safely against the session's specific parser.
    /// </summary>
    /// <param name="input">The raw command line text.</param>
    /// <param name="context">The session executing the command.</param>
    /// <returns>A strongly typed response representing the command's standard output or error.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the provided session context is null.</exception>
    public async Task<CommandResponse> ExecuteAsync(string input, SessionContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        // get or allocate the session state atomically
        var state = _sessionStates.GetValue(context, ctx => 
            new SessionRouterState(GetCommandLineConfiguration(ctx), new SemaphoreSlim(1, 1)));

        // instantiate per-execution to prevent shared state corruption
        var outputCapture = new CommandResultCapture();
        var outputErrCapture = new CommandResultCapture { ResultType = CommandResponse.Type.Error };

        // synchronize execution per session
        await state.ExecutionLock.WaitAsync();
        try
        {
            state.CommandParser.Output = outputCapture;
            state.CommandParser.Error = outputErrCapture;

            var exitCode = await state.CommandParser.InvokeAsync(input);
            
            return exitCode != 0 
                ? outputErrCapture.GetCommandResponse() 
                : outputCapture.GetCommandResponse();
        }
        catch (Exception ex)
        {
            return new CommandResponse(CommandResponse.Type.Error, ex.Message, ex.StackTrace);
        }
        finally
        {
            state.ExecutionLock.Release();
        }
    }
}