using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Maldact.Client.Networking;
using Maldact.Client.Networking.ServerCommands;

namespace Maldact.CLI.Commands.Results;

/// <summary>Settings to fetch explicitly targeted inference logs.</summary>
public sealed class ResultsGetSettings : GlobalCommandSettings
{
    /// <summary>Gets the list of requested event IDs.</summary>
    [CommandArgument(0, "[result-ids]")]
    public string[] ResultIds { get; init; } = [];
}

/// <summary>
/// Requests and prints classification results matching provided IDs.
/// </summary>
public sealed class ResultsGetCommand : BaseResultsCommand<ResultsGetSettings>
{
    /// <inheritdoc />
    protected override Task<ServerResponse> SendServerRequestAsync(MaldactClient client, ResultsGetSettings settings) =>
        client.SendControlCommandAsync(new ResultsGetServerCommand(settings.ResultIds));
}