using System.Text;
using Maldact.Core.ML;
using Maldact.Core.Results;

namespace Maldact.Client.Networking.ServerCommands;

/// <summary>
/// The abstract base class for all control commands interacting with the remote result repository.
/// </summary>
public abstract class ResultsServerCommand(string commandText) : ServerCommand($"results {commandText}");

/// <summary>
/// Requests the most recently generated inference result from the server.
/// </summary>
public sealed class ResultsLatestServerCommand() : ResultsServerCommand("latest");

/// <summary>
/// Requests a complete list of all currently stored inference results.
/// </summary>
public sealed class ResultsListServerCommand() : ResultsServerCommand("list");

/// <summary>
/// Requests specific inference results by their unique identifiers.
/// </summary>
/// <param name="resultIds">The collection of result IDs to fetch.</param>
public sealed class ResultsGetServerCommand(params string[] resultIds) 
    : ResultsServerCommand($"get {string.Join(" ", resultIds)}");

/// <summary>
/// Commands the server to permanently delete specific results by their identifiers.
/// </summary>
/// <param name="resultIds">The collection of result IDs to delete.</param>
public sealed class ResultsDeleteServerCommand(params string[] resultIds) 
    : ResultsServerCommand($"delete {string.Join(" ", resultIds)}");

/// <summary>
/// Queries the server for results matching specific temporal or classification filters.
/// </summary>
/// <param name="resultQuery">The filtering criteria.</param>
public sealed class ResultsQueryServerCommand(ResultQuery resultQuery) 
    : ResultsServerCommand(CommandFormatter.FormatQuery("query", resultQuery));

/// <summary>
/// Commands the server to permanently purge results matching specific temporal or classification filters.
/// </summary>
/// <param name="resultQuery">The filtering criteria.</param>
public sealed class ResultsQueryDeleteServerCommand(ResultQuery resultQuery) 
    : ResultsServerCommand(CommandFormatter.FormatQuery("query delete", resultQuery));
    
    
/// <summary>
/// Contains internal formatting utilities for constructing valid CLI protocol strings.
/// </summary>
internal static class CommandFormatter
{
    /// <summary>
    /// Safely translates a ResultQuery object into a valid System.CommandLine argument string.
    /// </summary>
    /// <param name="action">The action string defined by the protocol</param>
    /// <param name="query">The filtering criteria.</param>
    /// <returns>The correctly formatted query command.</returns>
    public static string FormatQuery(string action, ResultQuery query)
    {
        var sb = new StringBuilder(action);

        if (query.MinTime.HasValue)
        {
            sb.Append($" --min {query.MinTime.Value}");
        }

        if (query.MaxTime.HasValue)
        {
            sb.Append($" --max {query.MaxTime.Value}");
        }

        if (query.Classes != null && query.Classes.Count > 0)
        {
            sb.Append(" --filter ").Append(string.Join(" ", query.Classes.Select(c => c.ClassName)));
        }

        return sb.ToString();
    }
}