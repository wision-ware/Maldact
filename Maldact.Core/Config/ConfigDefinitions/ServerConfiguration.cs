namespace Maldact.Core.Config.ConfigDefinitions;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Defines the network, security, and operational boundaries of the server runtime.
/// </summary>
public record ServerConfiguration
{
    /// <summary>
    /// Gets the globally unique identifier for this specific node.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "ServerId is required to uniquely identify this node.")]
    public string ServerId { get; init; } = string.Empty;
    
    /// <summary>
    /// Gets the collection of raw string keys granted administrative privileges.
    /// </summary>
    [Required]
    public HashSet<string> AdminKeys { get; init; } = new();

    /// <summary>
    /// Gets the collection of raw string keys granted standard user privileges.
    /// </summary>
    [Required]
    public HashSet<string> UserKeys { get; init; } = new();
    
    /// <summary>
    /// Gets the maximum number of results to retain in memory before purging the oldest entries.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Backlog count must be a valid positive number.")]
    public int? MaxResultBacklogCount { get; init; } = 10000;

    /// <summary>
    /// Gets the maximum chronological age of a result in memory before it is purged.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? MaxResultRetentionMinutes { get; init; } = 60;
}