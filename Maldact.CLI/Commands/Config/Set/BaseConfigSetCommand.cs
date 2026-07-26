using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Maldact.CLI.Commands.Config.Set;

/// <summary>
/// Provides an optimized abstract platform orchestrating boilerplate path evaluation, schema checking, and state generation.
/// </summary>
/// <typeparam name="TSettings">The structural command settings mapping metadata inputs.</typeparam>
/// <typeparam name="TConfig">The strongly typed configuration schema class being targeted.</typeparam>
public abstract class BaseConfigSetCommand<TSettings, TConfig> : AsyncCommand<TSettings> 
    where TSettings : SetSettings 
    where TConfig : class, new()
{
    /// <summary>
    /// Gets the clear display label of the configuration subsystem block.
    /// </summary>
    protected abstract string SubsystemLabel { get; }

    /// <summary>
    /// Applies structural record updates to the target property slot within the current configuration index layer.
    /// </summary>
    protected abstract ConfigurationIndex ApplyMutation(ConfigurationIndex baseIndex, string? verifiedPath);

    /// <inheritdoc />
    protected sealed override Task<int> ExecuteAsync(CommandContext context, TSettings settings, CancellationToken ct)
    {
        // verify the structural reset signal sequence before performing schema structural calls
        bool isResetSequence = string.Equals(settings.ConfigFilePath, "null", StringComparison.OrdinalIgnoreCase);
        string? resolvedPath = isResetSequence ? null : Path.GetFullPath(settings.ConfigFilePath);

        if (resolvedPath != null && !GlobalConfigurationIndexManager.Verify<TConfig>(resolvedPath))
        {
            Terminal.Error.MarkupLine($"[bold red]Error:[/] The provided path for [yellow]{SubsystemLabel}[/] does not contain a valid schema format.");
            return Task.FromResult(1);
        }

        var currentIndex = GlobalConfigurationIndexManager.GetCurrentState() 
                           ?? new ConfigurationIndex(null, null, null, null);

        var updatedIndex = ApplyMutation(currentIndex, resolvedPath);
        GlobalConfigurationIndexManager.SetNewState(updatedIndex);

        string actionMessage = resolvedPath == null ? "cleared from" : "assigned within";
        Terminal.Out.MarkupLine($"[bold green]✔[/] [white]{SubsystemLabel}[/] destination route has been successfully {actionMessage} the configuration index.");
        
        return Task.FromResult(0);
    }
}