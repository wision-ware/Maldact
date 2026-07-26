using System.ComponentModel;
using System.Text.Json;
using Maldact.Backend.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;
using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Maldact.Client.Networking;
using Maldact.Client.Networking.ServerCommands;
using SkiaSharp;

namespace Maldact.CLI.Commands.Server;

/// <summary>
/// Defines the command-line options required to fetch and export server operational status.
/// </summary>
public sealed class ServerStatusSettings : GlobalCommandSettings
{
    /// <summary>
    /// Initializes the target path to export the server diagnostics snapshot to a file.
    /// </summary>
    [CommandOption("-o|--output")]
    [Description("Output target path to export raw plain JSON diagnostics metrics.")]
    public string? OutputJson { get; init; }
}

/// <summary>
/// Connects to the active server environment to retrieve, pretty-print, or export real-time diagnostics tracking metrics using source-generated serialization.
/// </summary>
public sealed class ServerStatusCommand : AsyncCommand<ServerStatusSettings>
{
    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, ServerStatusSettings settings, CancellationToken ct)
    {
        Terminal.Error.MarkupLine("[green]Querying server runtime diagnostics snapshot...[/]");
        
        var connection = GlobalConnectionStateIndexManager.GetCurrentState();
        if (connection == null) 
        {
            Terminal.Error.MarkupLine($"[bold red]Connection Error:[/] Active network connection path index is missing. Execute the [grey]connect[/] command first.\"");
            return -1;
        }

        await using var client = await MaldactClient.ConnectAsync(connection, ct);
        var response = await client.SendControlCommandAsync(new StatusServerCommand(), ct);

        string rawJsonPayload = (response.Payload ?? "{}");

        try
        {
            // maximize source generator optimizations to catch structural corruption before file manipulation starts
            ServerDiagnosticMetrics metrics = DiagnosticMetricsSerializer.DeserializeFromString(rawJsonPayload);

            if (!settings.OutputJson.MakeSafeOrEmpty())
            {
                var directory = Path.GetDirectoryName(settings.OutputJson);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // directly blits pre-compiled source-generated utf8 bytes down to the unmanaged file stream
                byte[] optimizedBytes = DiagnosticMetricsSerializer.SerializeToUtf8Bytes(metrics);
                await File.WriteAllBytesAsync(settings.OutputJson!, optimizedBytes);
                
                Terminal.Out.MarkupLine($"[bold green]✔ Diagnostics snapshot exported successfully to:[/] [white]{settings.OutputJson}[/]");
                return 0;
            }

            // pipe verified model objects into our custom dual-zone display extensions
            Terminal.Out.Write(metrics.RenderDashboard());
            return 0;
        }
        catch (JsonException ex)
        {
            Terminal.Error.MarkupLine("[bold red]Payload Error:[/] Received unparseable or corrupted payload from the server.");
            Terminal.Error.MarkupLine($"[red]Error details:[/] {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Terminal.Error.MarkupLine("[bold red]Unexpected Error[/]");
            Terminal.Error.MarkupLine($"[red]Error details:[/] {ex.Message}");
            return 1;
        }
    }
}

/// <summary>
/// Core internal string optimization extensions.
/// </summary>
internal static class StringOptimizationExtensions
{
    public static bool MakeSafeOrEmpty(this string? value) => string.IsNullOrWhiteSpace(value);
}