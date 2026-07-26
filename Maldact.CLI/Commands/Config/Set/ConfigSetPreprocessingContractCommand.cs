using Maldact.Client.Indexing;
using Maldact.Core.Config.ConfigDefinitions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Maldact.CLI.Commands.Config.Set;

/// <summary>
/// Settings specialized for Preprocessing Contract updates.
/// </summary>
public sealed class ConfigSetPreprocessingContractSettings : SetSettings;

/// <summary>
/// Assigns, validates or purges the indexed configuration file.
/// </summary>
public sealed class ConfigSetPreprocessingContractCommand : BaseConfigSetCommand<ConfigSetPreprocessingContractSettings, PreprocessingContract>
{
    /// <inheritdoc />
    protected override string SubsystemLabel => "Preprocessing Contract";

    /// <inheritdoc />
    protected override ConfigurationIndex ApplyMutation(ConfigurationIndex baseIndex, string? verifiedPath) =>
        baseIndex with { PreprocessingContractPath = verifiedPath };
}