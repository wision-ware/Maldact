using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Maldact.Core.Config.ConfigDefinitions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Maldact.CLI.Commands.Config;

/// <summary>
/// Shared baseline settings options for master index inspection command.
/// </summary>
public sealed class ConfigListSettings : GlobalCommandSettings
{
}

/// <summary>
/// Renders a diagnostic overview table of all registered system configuration states alongside active structural validation flags.
/// </summary>
public sealed class ConfigListCommand : AsyncCommand<ConfigListSettings>
{
    /// <inheritdoc />
    protected override Task<int> ExecuteAsync(CommandContext context, ConfigListSettings settings, CancellationToken ct)
    {
        var index = GlobalConfigurationIndexManager.GetCurrentState();
        if (index == null)
        {
            Terminal.Error.MarkupLine("[bold yellow]⚠️ No configuration index file initialization recorded in the current environment context.[/]");
            return Task.FromResult(0);
        }

        var table = new Table().Expand().Border(TableBorder.Rounded).BorderColor(Color.Grey);
        table.Title("[bold white]MALDACT SYSTEM CONFIGURATION SUMMARY[/]");
        table.AddColumn("[bold grey]Component Target[/]");
        table.AddColumn("[bold grey]File Path[/]");
        table.AddColumn("[bold grey]Status[/]");

        // populate configuration components systematically using embedded generic runtime schema checks
        AddDiagnosticRow<ServerConfiguration>(table, "Server Configuration", index.ServerConfigurationPath);
        AddDiagnosticRow<PreprocessingContract>(table, "Preprocessing Contract", index.PreprocessingContractPath);
        AddDiagnosticRow<TrainingConfiguration>(table, "Training Configuration", index.TrainingConfigurationPath);
        AddDiagnosticRow<ModelSpecification>(table, "Model Specification", index.ModelSpecificationPath);

        Terminal.Out.Write(table);
        return Task.FromResult(0);
    }

    private static void AddDiagnosticRow<TConfig>(Table table, string componentName, string? path) where TConfig : class, new()
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            table.AddRow(componentName, "[grey]Unallocated[/]", "[bold red]EMPTY[/]");
            return;
        }

        string resolvedPath = Path.GetFileName(path);
        
        // cross-reference index state against active disk content
        if (!File.Exists(path))
        {
            table.AddRow(componentName, $"[grey]{resolvedPath}[/]", "[bold yellow]NOT FOUND[/]");
            return;
        }

        bool isVerified = GlobalConfigurationIndexManager.Verify<TConfig>(path);
        string statusText = isVerified ? "[bold green]VERIFIED[/]" : "[bold red]CORRUPT/INVALID[/]";

        table.AddRow(componentName, $"[white]{path}[/]", statusText);
    }
}