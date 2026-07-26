using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Maldact.Backend.Server.ControlProtocol.Responses;
using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Maldact.Client.Networking;
using Maldact.Client.Networking.ServerCommands;

namespace Maldact.CLI.Commands.Results;

/// <summary>
/// Settings to specify discrete inference event logs for deletion.
/// </summary>
public sealed class ResultsDeleteSettings : GlobalCommandSettings
{
    /// <summary>
    /// Gets the discrete server event identification strings.
    /// </summary>
    [CommandArgument(0, "[result-ids]")]
    public string[] ResultIds { get; init; } = [];
}

/// <summary>
/// Removes specific detection events from the remote server by their IDs.
/// </summary>
public sealed class ResultsDeleteCommand : BaseResultsCommand<ResultsDeleteSettings>
{
    /// <inheritdoc />
    protected override Task<ServerResponse> SendServerRequestAsync(MaldactClient client, ResultsDeleteSettings settings) =>
        client.SendControlCommandAsync(new ResultsDeleteServerCommand(settings.ResultIds));
}