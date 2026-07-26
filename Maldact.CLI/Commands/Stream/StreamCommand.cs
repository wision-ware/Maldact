using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Globalization;
using Maldact.CLI.Common;
using Maldact.Client;
using Maldact.Client.Indexing;
using Maldact.Client.Networking;
using Maldact.Core.Results;

namespace Maldact.CLI.Commands.Stream;

/// <summary>
/// Defines the command-line arguments and flags accepted by the binary data streaming command.
/// </summary>
public sealed class StreamSettings : GlobalCommandSettings
{
    /// <summary>
    /// Initializes the optional initial temporal reference index for the synchronization header.
    /// </summary>
    [CommandOption("-t")] 
    public string? InitialTime { get; init; }
    
    /// <summary>
    /// Specify a file to read from instead of the standard input
    /// </summary>
    [CommandOption("-f|--file")] 
    public string? FilePath { get; init; }
}

/// <summary>
/// Orchestrates zero-allocation, high-throughput ingestion of raw binary telemetry streams from standard input.
/// </summary>
public sealed class StreamCommand : AsyncCommand<StreamSettings>
{
    /// <summary>
    /// Function pointer used to fetch the standard input stream. 
    /// Defaults to the raw OS pipe handle, but can be overridden during integration tests.
    /// </summary>
    internal static Func<System.IO.Stream> GetStandardInputStream { get; set; } = Console.OpenStandardInput;
    
    private const int TargetBufferBytes = 64 * 1024; 
    
    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, StreamSettings settings, CancellationToken ct)
    {
        var state = GlobalConnectionStateIndexManager.GetCurrentState();
        if (state == null) 
        {
            Terminal.Error.MarkupLine($"[bold red]Connection Error:[/] Active network connection path index is missing. " +
                                      $"Execute the [grey]connect[/] command first.");
            return -1;
        }

        var t0 = settings.InitialTime != null 
            ? new StreamTime(settings.InitialTime) 
            : new StreamTime(DateTime.UtcNow);
            
        Terminal.Error.MarkupLine("[grey]Establishing secure streaming channel...[/]");
        await using var client = await MaldactClient.ConnectAsync(state, ct);
        await client.ConnectStreamChannelAsync(t0, ct);

        await using var stdin = settings.FilePath != null 
            ? File.OpenRead(settings.FilePath) 
            : GetStandardInputStream();
            
        string sourceName = settings.FilePath != null ? $"file '{settings.FilePath}'" : "stdin";
        Terminal.Error.MarkupLine($"[green]Ingesting raw binary stream from {sourceName}...[/]");

        byte[] byteBuffer = new byte[TargetBufferBytes];
        float[] floatBuffer = new float[TargetBufferBytes/sizeof(float)];

        long totalBytesIngested = 0; // track the lifetime ingestion

        try
        {
            while (!ct.IsCancellationRequested)
            {
                int bytesRead = 0;
                while (bytesRead < TargetBufferBytes)
                {
                    int read = await stdin.ReadAsync(byteBuffer.AsMemory(bytesRead, TargetBufferBytes - bytesRead), ct);
                    
                    if (read == 0) 
                    {
                        // warn user in case of instant pipe disconnect
                        if (totalBytesIngested == 0 && bytesRead == 0)
                        {
                            Terminal.Error.MarkupLine("\n[bold red]✖ FATAL: Stream disconnected instantly with 0 bytes read.[/]");
                            Terminal.Error.MarkupLine("[bold yellow]Diagnostic Hint:[/] If you are piping binary data via Windows PowerShell (using '<' or '|'), the shell is likely corrupting or blocking the stream by interpreting it as text.");
                            Terminal.Error.MarkupLine("To fix this, either:");
                            Terminal.Error.MarkupLine("  1. Use the explicit file argument: [grey]maldact stream -f ./data.bin[/]");
                            Terminal.Error.MarkupLine("  2. Run the pipeline in a raw Bash/Unix environment.");
                            return -1;
                        }
                        
                        if (bytesRead > 0)
                        {
                            int validFloats = bytesRead / sizeof(float);
                            float[] finalBuffer = new float[validFloats];
                            Buffer.BlockCopy(byteBuffer, 0, finalBuffer, 0, bytesRead);
                            
                            await client.SendDataAsync(finalBuffer, ct);
                        }
                        
                        Terminal.Error.MarkupLine($"\n[yellow]Stream source disconnected. Total bytes ingested:[/] [white]{totalBytesIngested}[/]");
                        await Task.Delay(500, CancellationToken.None);
                        return 0; 
                    }
                    
                    bytesRead += read;
                    totalBytesIngested += read;
                }

                Buffer.BlockCopy(byteBuffer, 0, floatBuffer, 0, TargetBufferBytes);
                await client.SendDataAsync(floatBuffer, ct);
            }
        }
        catch (OperationCanceledException)
        {
            Terminal.Error.MarkupLine("\n[yellow]Streaming operation cancelled.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            Terminal.Error.MarkupLine("[bold red]Unexpected Error during streaming[/]");
            Terminal.Error.MarkupLine($"[red]Error details:[/] {ex.Message}");
            return 1;
        }

        return 0;
    }
}