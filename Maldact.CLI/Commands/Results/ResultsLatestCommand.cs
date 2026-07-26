using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Maldact.Client.Networking;
using Maldact.Client.Networking.ServerCommands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Maldact.CLI.Commands.Results;

/// <summary>
/// Settings for latest result retrieval.
/// </summary>
public class ResultsLatestSettings : GlobalCommandSettings
{
    
}

/// <summary>
/// Prints out the latest inference result.
/// </summary>
public sealed class ResultsLatestCommand : BaseResultsCommand<ResultsLatestSettings>
{
    /// <inheritdoc />
    protected override Task<ServerResponse> SendServerRequestAsync(MaldactClient client, ResultsLatestSettings settings) =>
        client.SendControlCommandAsync(new ResultsLatestServerCommand());
}