using Maldact.Client.Indexing;
using Maldact.Core.Config.ConfigDefinitions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Maldact.CLI.Commands.Config.Set;

/// <summary>
/// Settings specialized for Training Configuration updates.
/// </summary>
public sealed class ConfigSetTrainingConfigurationSettings : SetSettings;

/// <summary>
/// Assigns, validates or purges the indexed configuration file.
/// </summary>
public sealed class ConfigSetTrainingConfigurationCommand : BaseConfigSetCommand<ConfigSetTrainingConfigurationSettings, TrainingConfiguration>
{
    /// <inheritdoc />
    protected override string SubsystemLabel => "Training Configuration";

    /// <inheritdoc />
    protected override ConfigurationIndex ApplyMutation(ConfigurationIndex baseIndex, string? verifiedPath) =>
        baseIndex with { TrainingConfigurationPath = verifiedPath };
}