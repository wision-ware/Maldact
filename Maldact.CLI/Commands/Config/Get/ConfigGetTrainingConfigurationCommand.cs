using Maldact.Client.Indexing;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Maldact.CLI.Commands.Config.Get;

/// <summary>
/// Settings container for getting the Training Configuration path.
/// </summary>
public sealed class ConfigGetTrainingConfigurationSettings : GetSettings;

/// <summary>
/// Locates and inspects the indexed config file path pointing to the indexed Training Configuration.
/// </summary>
public sealed class ConfigGetTrainingConfigurationCommand : AsyncCommand<ConfigGetTrainingConfigurationSettings>
{
    /// <inheritdoc />
    protected override Task<int> ExecuteAsync(CommandContext context, ConfigGetTrainingConfigurationSettings settings, CancellationToken ct)
    {
        var path = GlobalConfigurationIndexManager.GetCurrentState()?.TrainingConfigurationPath;
        ConfigGetModelSpecificationCommand.RenderSingleComponentPath("Model Pipeline Training Criteria", path);
        return Task.FromResult(0);
    }
}