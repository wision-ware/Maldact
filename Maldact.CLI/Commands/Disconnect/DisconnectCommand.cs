using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Maldact.Client.Networking;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Maldact.CLI.Commands.Disconnect;

/// <summary>
/// Synchronously terminates and purges the active remote network session configuration indices on the host system.
/// </summary>
public sealed class DisconnectCommand : Command
{
    /// <inheritdoc />
    protected override int Execute(CommandContext context, CancellationToken ct)
    {
        // execute synchronously to eliminate useless task lifecycle generation states
        GlobalConnectionStateIndexManager.SetNewState(null);
        Terminal.Out.MarkupLine("[bold green]✔ Successfully disconnected from the active server gateway profile.[/]");
        return 0;
    }
}