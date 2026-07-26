using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Maldact.CLI.Commands.Config.Get;

/// <summary>
/// Settings container for getting the Model Specification path.
/// </summary>
public sealed class ConfigGetModelSpecificationSettings : GetSettings;

/// <summary>
/// Locates and inspects the indexed config file path pointing to the indexed Model Specification.
/// </summary>
public sealed class ConfigGetModelSpecificationCommand : AsyncCommand<ConfigGetModelSpecificationSettings>
{
    /// <inheritdoc />
    protected override Task<int> ExecuteAsync(CommandContext context, ConfigGetModelSpecificationSettings settings, CancellationToken ct)
    {
        var path = GlobalConfigurationIndexManager.GetCurrentState()?.ModelSpecificationPath;
        RenderSingleComponentPath("Neural Network Topology Specification", path);
        return Task.FromResult(0);
    }

    internal static void RenderSingleComponentPath(string label, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Terminal.Error.MarkupLine($"[grey]{label}:[/] [bold red]No configuration target file indexed.[/]");
            return;
        }
        Terminal.Out.MarkupLine($"[grey]{label} target path:[/] [bold white]{path}[/]");
    }
}