using System.CommandLine;
using Maldact.Backend.Server.Streaming;
using Maldact.Core.ML;
using Maldact.Core.Results;
using Maldact.Core.Server;

namespace Maldact.Backend.Server.ControlProtocol.Commands;

/// <summary>
/// Defines the strictly hierarchical control protocol commands for interacting with result repositories.
/// </summary>
internal class ResultsCommands : CommandWrapper
{
    private const string RepositoryNotFound = "No existing result repository found!";

    /// <summary>
    /// Gets the root command node for the results tree.
    /// </summary>
    public sealed override Command Command { get; protected set; }

    /// <summary>
    /// Initializes a new instance of the results command tree bound to a specific session context.
    /// </summary>
    /// <param name="context">The execution context for the current session.</param>
    public ResultsCommands(SessionContext context) : base(context)
    {
        var latest = new Command("latest", "Fetch the most recent result.");
        latest.SetAction(p => ExecuteAsync(p, LatestHandler));

        var get = new Command("get", "Fetch specific results by their identifiers.")
        {
            new Argument<string[]>("result-ids")
        };
        get.SetAction(p => ExecuteAsync(p, GetHandler));

        var delete = new Command("delete", "Delete specific results by their identifiers.")
        {
            new Argument<string[]>("result-ids")
        };
        delete.SetAction(p => ExecuteAsync(p, DeleteHandler));
        
        // explicit arrays ensure descriptions are preserved in the CLI help system
        var minOption = new Option<string?>("--min-timestamp", "-m", "--min")
        {
            Description = "The minimum timestamp threshold."
        };

        var maxOption = new Option<string?>("--max-timestamp", "-x", "--max")
        {
            Description = "The maximum timestamp threshold."
        };

        var filterOption = new Option<string[]?>("--filter-classes", "-f", "--filter")
        {
            Description = "Specific classification classes to filter by.",
            AllowMultipleArgumentsPerToken = true
        };

        var query = new Command("query", "Query results based on temporal boundaries and classifications.")
        {
            minOption, maxOption, filterOption
        };
        query.SetAction(p => ExecuteAsync(p, QueryHandler));
        
        var queryDelete = new Command("delete", "Purge results based on temporal boundaries and classifications.")
        {
            minOption, maxOption, filterOption
        };
        queryDelete.SetAction(p => ExecuteAsync(p, QueryDeleteHandler));

        query.Add(queryDelete);

        Command = new Command("results", "Commands for managing inference result repositories.")
        {
            latest, get, delete, query
        };
    }

    /// <summary>
    /// A centralized execution pipeline that guards all repository commands with strict authentication and null checks.
    /// </summary>
    private async Task ExecuteAsync(ParseResult parseResult, Func<ParseResult, StreamingSlot, Task> internalHandler)
    {
        try
        {
            var pass = Context.AccessPass ?? throw new UnauthorizedAccessException();
            
            if (!Context.TryGetSlot(pass, out var slot) || slot == null) 
            {
                throw new InvalidOperationException(RepositoryNotFound);
            }

            await internalHandler(parseResult, slot);
        }
        catch (UnauthorizedAccessException)
        {
            await parseResult.Configuration.Error.WriteLineAsync(UnauthorizedAccessMessage);
        }
        catch (InvalidOperationException ex)
        {
            await parseResult.Configuration.Error.WriteLineAsync(ex.Message);
        }
    }

    private async Task LatestHandler(ParseResult parseResult, StreamingSlot slot)
    {
        var result = await slot.Repository.GetLatestAsync();
        if (result is not null) await OutputResults([result], parseResult);
    }
    
    private async Task GetHandler(ParseResult parseResult, StreamingSlot slot)
    {
        var resultIds = parseResult.GetValue<string[]>("result-ids");
        if (resultIds is not null && resultIds.Length > 0) 
        {
            var results = await slot.Repository.GetAsync(resultIds);
            await OutputResults(results, parseResult);
        }
    }
    
    private async Task DeleteHandler(ParseResult parseResult, StreamingSlot slot)
    {
        var resultIds = parseResult.GetValue<string[]>("result-ids");
        if (resultIds is not null && resultIds.Length > 0) 
        {
            await slot.Repository.DeleteAsync(resultIds);
        }
    }

    private async Task QueryHandler(ParseResult parseResult, StreamingSlot slot)
    {
        var query = BuildQueryFromOptions(parseResult);
        var results = await slot.Repository.QueryAsync(query);
        await OutputResults(results, parseResult);
    }

    private async Task QueryDeleteHandler(ParseResult parseResult, StreamingSlot slot)
    {
        var query = BuildQueryFromOptions(parseResult);
        await slot.Repository.QueryDeleteAsync(query);
    }

    private async Task OutputResults(IEnumerable<ResultEntry> results, ParseResult parseResult)
    {
        await parseResult.Configuration.Output.WriteLineAsync($" {Environment.NewLine}");
        
        foreach (var result in results)
        {
            await parseResult.Configuration.Output.WriteLineAsync(result.ToString());
        }
    }
    
    private static ResultQuery BuildQueryFromOptions(ParseResult parseResult)
    {
        var startTimeString = parseResult.GetValue<string?>("--min-timestamp");
        var endTimeString = parseResult.GetValue<string?>("--max-timestamp");
        var classNames = parseResult.GetValue<string[]?>("--filter-classes");

        var startTime = startTimeString is not null ? new StreamTime(startTimeString) : (StreamTime?)null;
        var endTime = endTimeString is not null ? new StreamTime(endTimeString) : (StreamTime?)null;
        
        var classes = classNames?.Length > 0 
            ? classNames.Select(name => new ClassificationClass(name)).ToHashSet() 
            : null;

        return new ResultQuery
        {
            Classes = classes,
            MinTime = startTime,
            MaxTime = endTime
        };
    }
}