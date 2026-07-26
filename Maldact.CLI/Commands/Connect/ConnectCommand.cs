using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Net.Sockets;
using System.Security.Authentication;
using Maldact.CLI.Common;
using Maldact.Client;
using Maldact.Client.Indexing;
using Maldact.Client.Networking;
using Maldact.Core.Server.Authentication;

namespace Maldact.CLI.Commands.Connect;

/// <summary>
/// Configuration settings for parsing connection arguments.
/// </summary>
public sealed class ConnectSettings : GlobalCommandSettings
{
    /// <summary>
    /// Gets the hostname or IP address of the target server.
    /// </summary>
    [CommandArgument(0, "[host]")]
    public string Host { get; init; } = string.Empty;

    /// <summary>
    /// Gets the control channel port of the target server.
    /// </summary>
    [CommandArgument(1, "[port]")]
    public int Port { get; init; } = 5000;

    /// <summary>
    /// Gets the security token required for server authentication.
    /// </summary>
    [CommandArgument(2, "[authToken]")] 
    public string AuthToken { get; init; } = string.Empty;
}

/// <summary>
/// Validates server availability and credentials before saving the connection index.
/// </summary>
public sealed class ConnectCommand : AsyncCommand<ConnectSettings>
{
    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, ConnectSettings settings, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            Terminal.Error.MarkupLine("[bold red]Error:[/] Server host address cannot be empty.");
            return -1;
        }

        Terminal.Error.MarkupLine($"[grey]Attempting to connect to {settings.Host}:{settings.Port}...[/]");

        var connection = new ConnectionStateIndex
        {
            Host = settings.Host,
            Port = settings.Port,
            AuthToken = new AuthToken(settings.AuthToken)
        };

        try
        {
            // wrap client in a disposal block to immediately drop the socket after validation
            await using var client = await MaldactClient.ConnectAsync(connection);
            
            // save the active connection configuration only if the authentication pass succeeds
            GlobalConnectionStateIndexManager.SetNewState(connection);
            
            Terminal.Out.MarkupLine("[bold green]✔ Connection verified and saved successfully.[/]");
            return 0;
        }
        catch (AuthenticationException)
        {
            Terminal.Error.MarkupLine("[bold red]Authentication Failed:[/] The server rejected the provided token.");
            return 1;
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException or IOException)
        {
            Terminal.Error.MarkupLine($"[bold red]Network Error:[/] Could not reach the server at {settings.Host}:{settings.Port}.");
            return 1;
        }
        catch (Exception ex)
        {
            Terminal.Error.MarkupLine("[bold red]Connection Error:[/] Communication with the remote server was lost.");
            Terminal.Error.MarkupLine($"[red]Error details:[/] {ex.Message}");
            return 1;
        }
    }
}