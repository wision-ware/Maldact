using Maldact.CLI.Common;
using Maldact.Core.ML;
using Maldact.Core.Results;
using Spectre.Console.Cli;

namespace Maldact.CLI.Commands.Results;

/// <summary>
/// Shared options for parsing query command line arguments and converting them into query objects.
/// </summary>
public class QuerySettings : GlobalCommandSettings
{
    /// <summary>
    /// Gets the minimum timestamp.
    /// </summary>
    [CommandOption("-m|--min|--min-timestamp <minimumTime>")]
    public string? MinimumTime { get; init; }
    
    /// <summary>
    /// Gets the maximum timestamp.
    /// </summary>
    [CommandOption("-x|--max|--max-timestamp <maximumTime>")]
    public string? MaximumTime { get; init; }
    
    /// <summary>
    /// Gets individual classes to isolate.
    /// </summary>
    [CommandOption("-f|--filter|--filter-classes <classNames>")]
    public string[]? FilteredClasses { get; init; }
    
    /// <summary>
    /// Assembles setting inputs into a query DTO.
    /// </summary>
    public ResultQuery Query => new()
    {
        MinTime = MinimumTime != null ? new StreamTime(MinimumTime) : null,
        MaxTime = MaximumTime != null ? new StreamTime(MaximumTime) : null,
        Classes = FilteredClasses != null 
            ? new HashSet<ClassificationClass>(FilteredClasses.Select(c => new ClassificationClass(c))) 
            : null
    };
}