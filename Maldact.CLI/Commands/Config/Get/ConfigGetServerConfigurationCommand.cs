using Maldact.Client.Indexing;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Maldact.CLI.Commands.Config.Get;

/// <summary>
/// Settings container for getting the Server Configuration path.
/// </summary>
public sealed class ConfigGetServerConfigurationSettings : GetSettings;

/// <summary>
/// Locates and inspects the indexed config file path pointing to the indexed Server Configuration.
/// </summary>
public sealed class ConfigGetServerConfigurationCommand : AsyncCommand<ConfigGetServerConfigurationSettings>
{
    /// <inheritdoc />
    protected override Task<int> ExecuteAsync(CommandContext context, ConfigGetServerConfigurationSettings settings, CancellationToken ct)
    {
        var path = GlobalConfigurationIndexManager.GetCurrentState()?.ServerConfigurationPath;
        ConfigGetModelSpecificationCommand.RenderSingleComponentPath("Backend Host Infrastructure Profile", path);
        return Task.FromResult(0);
    }
}