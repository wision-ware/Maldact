using Maldact.Client.Indexing;
using Maldact.Core.Config.ConfigDefinitions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Maldact.CLI.Commands.Config.Set;

/// <summary>
/// Settings specialized for Server Configuration updates.
/// </summary>
public sealed class ConfigSetServerConfigurationSettings : SetSettings;

/// <summary>
/// Assigns, validates or purges the indexed configuration file.
/// </summary>
public sealed class ConfigSetServerConfigurationCommand : BaseConfigSetCommand<ConfigSetServerConfigurationSettings, ServerConfiguration>
{
    /// <inheritdoc />
    protected override string SubsystemLabel => "Server Configuration";

    /// <inheritdoc />
    protected override ConfigurationIndex ApplyMutation(ConfigurationIndex baseIndex, string? verifiedPath) =>
        baseIndex with { ServerConfigurationPath = verifiedPath };
}