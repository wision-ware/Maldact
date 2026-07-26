using Maldact.Client.Indexing;
using Maldact.Client.Networking;
using Maldact.Client.Networking.ServerCommands;
using Maldact.Core.ML;
using Maldact.Core.Results;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Maldact.CLI.Commands.Results;

/// <summary>
/// Settings for queried result retrieval.
/// </summary>
public sealed class ResultsQuerySettings : QuerySettings;

/// <summary>
/// Requests and prints out matching results based on the query.
/// </summary>
public sealed class ResultsQueryCommand : BaseResultsCommand<ResultsQuerySettings>
{
    /// <inheritdoc />  
    protected override Task<ServerResponse> SendServerRequestAsync(MaldactClient client, ResultsQuerySettings settings) =>
        client.SendControlCommandAsync(new ResultsQueryServerCommand(settings.Query));
}