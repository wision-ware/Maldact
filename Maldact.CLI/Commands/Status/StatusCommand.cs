using System.Net.Sockets;
using System.Security.Authentication;
using Maldact.Backend.Diagnostics;
using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Maldact.Client.Networking;
using Maldact.Client.Networking.ServerCommands;
using Maldact.Core.Config.ConfigDefinitions;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

namespace Maldact.CLI.Commands.Status;

/// <summary>
/// Defines command-line options and execution arguments for index overview requests.
/// </summary>
public sealed class StatusSettings : GlobalCommandSettings
{
}

/// <summary>
/// Compiles local asset mappings, network routing contexts, and live server diagnostics into a unified matrix view.
/// </summary>
public sealed class StatusCommand : AsyncCommand<StatusSettings>
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(4);

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, StatusSettings settings, CancellationToken ct)
    {
        var configIndex = GlobalConfigurationIndexManager.GetCurrentState();
        var connectionIndex = GlobalConnectionStateIndexManager.GetCurrentState();

        var masterLayoutGrid = new Grid().AddColumns(2).Expand();

        // 1. Build local workspace status view
        var configTable = new Table().NoBorder().HideHeaders().AddColumns("", "").Expand();
        if (configIndex == null)
        {
            configTable.AddRow("[yellow]No active configuration index file found on disk.[/]", "");
        }
        else
        {
            AddAssetDiagnosticRow<ServerConfiguration>(configTable, "Server Configuration", configIndex.ServerConfigurationPath);
            AddAssetDiagnosticRow<PreprocessingContract>(configTable, "Preprocessing Contract", configIndex.PreprocessingContractPath);
            AddAssetDiagnosticRow<TrainingConfiguration>(configTable, "Training Configuration", configIndex.TrainingConfigurationPath);
            AddAssetDiagnosticRow<ModelSpecification>(configTable, "Model Specification", configIndex.ModelSpecificationPath);
        }

        // 2. Process and build the network connection and server diagnostics view
        var networkTable = new Table().NoBorder().HideHeaders().AddColumns("", "").Expand();
        IRenderable? remoteTelemetryWidget = null;

        if (connectionIndex == null)
        {
            networkTable.AddRow("[grey]Session Status:[/]", "[bold yellow]DISCONNECTED (No configuration found)[/]");
            networkTable.AddRow("[grey]Target Endpoint:[/]", "[grey]Unallocated[/]");
        }
        else
        {
            string rawToken = connectionIndex.AuthToken?.Value ?? string.Empty;
            string maskedToken = rawToken.Length > 8 ? $"***...{rawToken[^6..]}" : "********";

            networkTable.AddRow("[grey]Target Endpoint:[/]", $"[white]{connectionIndex.Host}:{connectionIndex.Port}[/]");
            networkTable.AddRow("[grey]Access Signature:[/]", $"[grey]{maskedToken}[/]");

            using var timeoutCts = new CancellationTokenSource(ProbeTimeout);

            // wrap connection and status verification loops into an isolated pipeline check
            try
            {
                await using var client = await MaldactClient.ConnectAsync(connectionIndex, timeoutCts.Token);
                networkTable.AddRow("[grey]Session Status:[/]", "[bold green]ONLINE (Authenticated successfully)[/]");

                var response = await client.SendControlCommandAsync(new StatusServerCommand(), timeoutCts.Token);
                string rawJsonPayload = (response.Payload ?? "{}").ToString();

                // leverage compile-time optimized layout engines directly
                ServerDiagnosticMetrics metrics = DiagnosticMetricsSerializer.DeserializeFromString(rawJsonPayload);
                remoteTelemetryWidget = metrics.RenderDashboard();
            }
            catch (AuthenticationException)
            {
                networkTable.AddRow("[grey]Session Status:[/]", "[bold red]UNAUTHORIZED (Access key rejected by host)[/]");
            }
            catch (Exception ex) when (ex is SocketException or TimeoutException or IOException)
            {
                networkTable.AddRow("[grey]Session Status:[/]", "[bold red]UNREACHABLE (Network link down or port closed)[/]");
            }
            catch (Exception ex)
            {
                networkTable.AddRow("[grey]Session Status:[/]", $"[bold red]FAULTED ({ex.Message})[/]");
            }
        }

        masterLayoutGrid.AddRow(
            new Panel(configTable).Header("[bold white] Local Configuration Index [/]").BorderColor(Color.Grey27),
            new Panel(networkTable).Header("[bold white] Server Connection [/]").BorderColor(Color.Grey27)
        );

        var mainContainer = new Table().Expand().Border(TableBorder.Rounded).BorderColor(Color.Grey);
        mainContainer.Title("[bold white]MALDACT WORKSPACE STATUS[/]");
        mainContainer.AddColumn(new TableColumn("").Centered());
        mainContainer.HideHeaders();
        mainContainer.AddRow(masterLayoutGrid);

        Terminal.Out.Write(mainContainer);

        // if a remote server handshake completes successfully, append its telemetry metrics layout view below the main frame
        if (remoteTelemetryWidget != null)
        {
            Terminal.Out.WriteLine();
            Terminal.Out.Write(remoteTelemetryWidget);
        }

        return 0;
    }

    private static void AddAssetDiagnosticRow<TConfig>(Table table, string label, string? path) where TConfig : class, new()
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            table.AddRow($"[grey]{label}:[/]", "[bold red]UNSET[/]");
            return;
        }

        if (!File.Exists(path))
        {
            table.AddRow($"[grey]{label}:[/]", "[bold yellow]FILE NOT FOUND[/]");
            return;
        }

        bool isValid = GlobalConfigurationIndexManager.Verify<TConfig>(path);
        string integrityMarker = isValid ? "[bold green]OK[/]" : "[bold red]CORRUPT[/]";

        table.AddRow($"[grey]{label}:[/]", $"{integrityMarker} [grey]({Path.GetFileName(path)})[/]");
    }
}