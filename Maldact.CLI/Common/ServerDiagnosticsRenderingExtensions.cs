using Maldact.Backend.Diagnostics;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Maldact.CLI.Common;

/// <summary>
/// Provides advanced visual layout engines for rendering server infrastructure health diagnostics.
/// </summary>
public static class ServerDiagnosticsRenderingExtensions
{
    /// <summary>
    /// Generates a structured, dual-zone dashboard layout mapping server activity metrics to terminal panels.
    /// </summary>
    /// <param name="metrics">The server diagnostic metrics.</param>
    /// <returns>A compound layout renderable object fit for live terminal views.</returns>
    public static IRenderable RenderDashboard(this ServerDiagnosticMetrics metrics)
    {
        var uptime = DateTimeOffset.Now - metrics.ServerStartTime;
        var uptimeString = $"{(int)uptime.TotalHours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";

        var grid = new Grid().AddColumns(2).Expand();

        // allocate explicit empty column headers to fulfill the table layout requirements
        var streamsStyle = metrics.ActiveStreamingSessions > 0 ? "[white]" : "[grey]";
        var sessionTable = new Table().NoBorder().HideHeaders().AddColumns("", "").Expand();
        sessionTable.AddRow("[grey]Active Ingestion Channels:[/]", $"{streamsStyle}{metrics.ActiveStreamingSessions}[/]");
        
        var repoStyle = metrics.HangingRepositories > 0 ? "[white]" : "[grey]";
        sessionTable.AddRow("[grey]Hanging Storage Handles:[/]", $"{repoStyle}{metrics.HangingRepositories}[/]");
        
        var recordsStyle = metrics.TotalCachedResults > 0 ? "[white]" : "[grey]";
        var resourceTable = new Table().NoBorder().HideHeaders().AddColumns("", "").Expand();
        resourceTable.AddRow("[grey]Total Telemetry Records:[/]", $"{recordsStyle}{metrics.TotalCachedResults}[/]");
        
        // calculate memory strictly using double primitives to prevent float truncation precision bugs
        double memoryMb = metrics.EstimatedMemoryUsageBytes / 1024.0 / 1024.0;
        var memoryStyle = memoryMb > 512.0 ? "[yellow]" : "[white]";
        resourceTable.AddRow("[grey]Estimated Results Allocation:[/]", $"{memoryStyle}{memoryMb:F2} MB[/]");

        grid.AddRow(
            new Panel(sessionTable).Header("[bold white] Network Session Layer [/]").BorderColor(Color.Grey27),
            new Panel(resourceTable).Header("[bold white] Performance & Storage [/]").BorderColor(Color.Grey27)
        );

        var containerTable = new Table().Expand().Border(TableBorder.Rounded).BorderColor(Color.Grey);
        containerTable.Title($"[bold white]MALDACT SERVER RUNTIME CONTROL PANEL[/] | [grey]UPTIME: {uptimeString}[/]");
    
        containerTable.AddColumn(new TableColumn("").Centered());
        
        containerTable.HideHeaders();
        
        containerTable.AddRow(grid);

        return containerTable;
    }
}