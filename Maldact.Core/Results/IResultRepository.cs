namespace Maldact.Core.Results;

/// <summary>
/// Defines the contract for persisting, querying, and managing inference result boundaries asynchronously.
/// </summary>
public interface IResultRepository
{
    /// <summary>
    /// Persists a batch of new result entries into the repository.
    /// </summary>
    /// <param name="results">The array of results to persist.</param>
    Task SaveAsync(ResultEntry[] results);

    /// <summary>
    /// Retrieves specific results by their unique identifiers.
    /// </summary>
    /// <param name="resultIds">The sequence of IDs to query.</param>
    /// <returns>The list of retreived results.</returns>
    Task<ResultEntry[]> GetAsync(string[] resultIds);

    /// <summary>
    /// Retrieves the most recently recorded prediction event, or null if the repository is empty.
    /// </summary>
    /// <returns>The latest result in the queue.</returns>
    Task<ResultEntry?> GetLatestAsync();

    /// <summary>
    /// Deletes specific results from the repository by their unique identifiers.
    /// </summary>
    /// <param name="resultIds">The IDs of the records to destroy.</param>
    Task DeleteAsync(string[] resultIds);

    /// <summary>
    /// Retrieves a collection of results that satisfy the specified temporal and classification filters.
    /// </summary>
    /// <param name="query">The boundary constraints for the query.</param>
    /// <returns>The queried results.</returns>
    Task<IEnumerable<ResultEntry>> QueryAsync(ResultQuery query);

    /// <summary>
    /// Deletes all results that satisfy the specified query parameters.
    /// </summary>
    /// <param name="query">The boundary constraints defining the deletion scope.</param>
    Task QueryDeleteAsync(ResultQuery query);

    /// <summary>
    /// Purges all result entries from the underlying storage.
    /// </summary>
    Task ClearAsync();
    
    /// <summary>
    /// Gets the total number of stored result entries.
    /// </summary>
    /// <returns>The total aggregate count.</returns>
    Task<int> GetCountAsync();
}