using Maldact.Client.Indexing;
using Maldact.Client.Networking;
using Maldact.Client.Networking.ServerCommands;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;

namespace Maldact.CLI.Commands.Results;

/// <summary>
/// Settings for queried result deletion.
/// </summary>
public sealed class ResultsQueryDeleteSettings : QuerySettings;

/// <summary>
/// Requests the deletion of matching results.
/// </summary>
public sealed class ResultsQueryDeleteCommand : BaseResultsCommand<ResultsQueryDeleteSettings>
{
    /// <inheritdoc />
    protected override Task<ServerResponse> SendServerRequestAsync(MaldactClient client, ResultsQueryDeleteSettings settings) =>
        client.SendControlCommandAsync(new ResultsQueryDeleteServerCommand(settings.Query));
}