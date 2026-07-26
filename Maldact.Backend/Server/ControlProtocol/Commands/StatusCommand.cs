using System.CommandLine;
using Maldact.Backend.Diagnostics;
using Maldact.Core.Server;

namespace Maldact.Backend.Server.ControlProtocol.Commands;

/// <summary>
/// Handles the retrieval of server diagnostic and health metrics securely.
/// </summary>
internal class StatusCommand : CommandWrapper
{
    /// <summary>
    /// Gets the root command node for status reporting.
    /// </summary>
    public sealed override Command Command { get; protected set; }

    /// <summary>
    /// Initializes a new instance of the status command.
    /// </summary>
    /// <param name="context">The execution context for the current session.</param>
    public StatusCommand(SessionContext context) : base(context)
    {
        Command = new Command("status", "Get server status info.");
        Command.SetAction(StatusHandler);
    }

    private async Task StatusHandler(ParseResult parseResult)
    {
        try
        {
            // enforce basic security to prevent anonymous information disclosure
            _ = Context.AccessPass ?? throw new UnauthorizedAccessException();

            var metrics = Context.GetServerDiagnosticMetrics()
                          ?? throw new InvalidDataException("No diagnostic data available.");
            
            await parseResult.Configuration.Output.WriteLineAsync(
                $" {Environment.NewLine}{DiagnosticMetricsSerializer.SerializeToString(metrics)}");
        }
        catch (UnauthorizedAccessException)
        {
            await parseResult.Configuration.Error.WriteLineAsync(UnauthorizedAccessMessage);
        }
        catch (InvalidDataException ex)
        {
            await parseResult.Configuration.Error.WriteLineAsync(ex.Message);
        }
    }
}