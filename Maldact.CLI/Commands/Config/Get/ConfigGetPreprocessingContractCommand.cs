using Maldact.Client.Indexing;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Maldact.CLI.Commands.Config.Get;

/// <summary>
/// Settings container for getting the Preprocessing Contract path.
/// </summary>
public sealed class ConfigGetPreprocessingContractSettings : GetSettings;

/// <summary>
/// Locates and inspects the indexed config file path pointing to the indexed Preprocessing Contract.
/// </summary>
public sealed class ConfigGetPreprocessingContractCommand : AsyncCommand<ConfigGetPreprocessingContractSettings>
{
    /// <inheritdoc />
    protected override Task<int> ExecuteAsync(CommandContext context, ConfigGetPreprocessingContractSettings settings, CancellationToken ct)
    {
        var path = GlobalConfigurationIndexManager.GetCurrentState()?.PreprocessingContractPath;
        ConfigGetModelSpecificationCommand.RenderSingleComponentPath("Signal Preprocessing Data Contract", path);
        return Task.FromResult(0);
    }
}