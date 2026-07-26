using Maldact.CLI.Common;
using Spectre.Console.Cli;

namespace Maldact.CLI.Commands.Config.Set;

/// <summary>
/// Baseline settings container defining arguments for uniform parameter mutations within the configuration indexer.
/// </summary>
public class SetSettings : GlobalCommandSettings
{
    /// <summary>
    /// Gets or initializes the target configuration path. Passing the literal string "null" purges this slot from the index.
    /// </summary>
    [CommandArgument(0, "[configFilePath]")]
    public string ConfigFilePath { get; init; } = string.Empty;
}