using Maldact.Client.Indexing;
using Maldact.Core.Config.ConfigDefinitions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Maldact.CLI.Commands.Config.Set;

/// <summary>
/// Settings specialized for Model Specification updates.
/// </summary>
public sealed class ConfigSetModelSpecificationSettings : SetSettings;

/// <summary>
/// Assigns, validates or purges the indexed configuration file.
/// </summary>
public sealed class ConfigSetModelSpecificationCommand : BaseConfigSetCommand<ConfigSetModelSpecificationSettings, ModelSpecification>
{
    /// <inheritdoc />
    protected override string SubsystemLabel => "Model Specification";

    /// <inheritdoc />
    protected override ConfigurationIndex ApplyMutation(ConfigurationIndex baseIndex, string? verifiedPath) =>
        baseIndex with { ModelSpecificationPath = verifiedPath };
}