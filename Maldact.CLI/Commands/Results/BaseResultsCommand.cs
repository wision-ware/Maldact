using System.ComponentModel;
using Maldact.Backend.Server.ControlProtocol.Responses;
using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Maldact.Client.Networking;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Maldact.CLI.Commands.Results;

/// <summary>
/// Base command class that unifies server connectivity, exception safety, and unescaped payload printing for all inference commands.
/// </summary>
/// <typeparam name="TSettings">The specific Spectre command settings record.</typeparam>
public abstract class BaseResultsCommand<TSettings> : AsyncCommand<TSettings> where TSettings : GlobalCommandSettings
{
    /// <inheritdoc />
    protected sealed override async Task<int> ExecuteAsync(CommandContext context, TSettings settings, CancellationToken ct)
    {
        Terminal.Error.MarkupLine("[grey]Connecting to active server control channel...[/]");
        
        var connection = GlobalConnectionStateIndexManager.GetCurrentState();
        if (connection == null)
        {
            Terminal.Error.MarkupLine("[bold red]Connection Error:[/] No active connection found. Run the connect command first.");
            return -1;
        }

        try
        { 
            await using var client = await MaldactClient.ConnectAsync(connection);
            
            Terminal.Error.MarkupLine("[grey]Sending request...[/]");
            var response = await SendServerRequestAsync(client, settings);

            switch (response.Type)
            {
                case ResponseType.Ok:
                    if (response?.Payload == null || string.IsNullOrWhiteSpace(response.Payload))
                    {
                        Terminal.Error.MarkupLine("[yellow]Empty response: No inference logs found matching your request.[/]");
                        return 0;
                    }
                    
                    Terminal.Out.WriteLine(response.Payload);
                    return 0;
                
                case ResponseType.Error:
                    Terminal.Error.MarkupLine($"[red]Server Error: {response.Message}[/]");
                    return 1;
                
                default:
                    Terminal.Error.MarkupLine("[red]Communication Error: Unknown response type[/]");
                    return -1;
            }
            
        }
        catch (Exception ex)
        {
            Terminal.Error.MarkupLine("[bold red]Connection Error:[/] Communication with the remote server was lost.");
            Terminal.Error.MarkupLine($"[red]Error details:[/] {ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// Sends the concrete request to the server.
    /// </summary>
    protected abstract Task<ServerResponse> SendServerRequestAsync(MaldactClient client, TSettings settings);
}