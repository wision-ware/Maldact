using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Maldact.Client.Networking;
using Maldact.Client.Networking.ServerCommands;

namespace Maldact.CLI.Commands.Server;

/// <summary>
/// Defines the options and configuration arguments for stopping a server instance.
/// </summary>
public sealed class ServerStopSettings : GlobalCommandSettings
{
}

/// <summary>
/// Dispatches a termination command asynchronously to the active server.
/// </summary>
public sealed class ServerStopCommand : AsyncCommand<ServerStopSettings>
{
    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, ServerStopSettings settings, CancellationToken ct)
    {
        var connection = GlobalConnectionStateIndexManager.GetCurrentState();
        if (connection == null)
        {
            Terminal.Error.MarkupLine($"[bold red]Connection Error:[/] Active network connection path index is missing. Execute the [grey]connect[/] command first.\"");
            return -1;
        }

        Terminal.Error.MarkupLine("[grey]Connecting to server control channel...[/]");

        try
        {
            await using var client = await MaldactClient.ConnectAsync(connection);
            
            Terminal.Error.MarkupLine("[grey]Sending stop command to the server...[/]");
            var response = await client.SendControlCommandAsync(new StopServerCommand());
            switch (response.Type)
            {
                case ResponseType.Ok:
                    Terminal.Out.MarkupLine("[bold green]✔ Server stop command completed successfully.[/]");
                    break;
                case ResponseType.Error:
                    Terminal.Error.MarkupLine($"[bold red]Error: {response.Message}[/]");
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unexpected response type: " + response.Type);
            }
            return 0;
        }
        catch (Exception ex)
        {
            Terminal.Error.MarkupLine("[bold red]Connection Error:[/] Communication with the remote server was lost.");
            Terminal.Error.MarkupLine($"[red]Error details:[/] {ex.Message}");
            return -1;
        }
    }
}