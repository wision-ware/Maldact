using Maldact.Core.Results;

namespace Maldact.Backend.Server.Results;

/// <summary>
/// Provides a highly optimized, thread-safe, in-memory repository for storing and querying time-series inference results.
/// </summary>
public sealed class InMemoryResultRepository : IResultRepository, IDisposable
{
    private readonly List<ResultEntry> _entries = new();
    private readonly Dictionary<string, ResultEntry> _entriesDictionary = new();
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    
    private bool _isDisposed;

    /// <inheritdoc />
    public Task SaveAsync(ResultEntry[] results)
    {
        if (results == null || results.Length == 0) return Task.CompletedTask;
        ThrowIfDisposed();

        _lock.EnterWriteLock();
        try
        {
            // guarantee chronological integrity for the binary search layer
            var orderedResults = results.OrderBy(r => r.StartTime).ToArray();

            // fast-path boundary check: if appending out of order compared to the tail, force a full sort
            bool requiresFullSort = _entries.Count > 0 && orderedResults[0].StartTime < _entries[^1].StartTime;

            _entries.AddRange(orderedResults);
            
            if (requiresFullSort)
            {
                _entries.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            }

            foreach (var result in orderedResults)
            {
                // safe upsert prevents duplicate ID crashes
                _entriesDictionary[result.Id] = result;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IEnumerable<ResultEntry>> QueryAsync(ResultQuery query)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
        ThrowIfDisposed();

        _lock.EnterReadLock();
        try
        {
            if (_entries.Count == 0) return Task.FromResult(Enumerable.Empty<ResultEntry>());

            int startIndex = query.MinTime.HasValue ? GetLowerBoundIndex(query.MinTime.Value) : 0;
            var results = new List<ResultEntry>();

            for (int i = startIndex; i < _entries.Count; i++)
            {
                var entry = _entries[i];

                if (query.MaxTime.HasValue && entry.StartTime > query.MaxTime.Value)
                    break;
                
                // skip ghost entries purged by discrete deletes
                if (!_entriesDictionary.ContainsKey(entry.Id))
                    continue;

                if (query.Classes == null || query.Classes.Contains(entry.Classification))
                {
                    results.Add(entry);
                }
            }

            return Task.FromResult<IEnumerable<ResultEntry>>(results);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public Task QueryDeleteAsync(ResultQuery query)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
        ThrowIfDisposed();

        _lock.EnterWriteLock();
        try
        {
            if (_entries.Count == 0) return Task.CompletedTask;

            // RemoveAll is O(N) but safely compacts both the list and the hash map
            _entries.RemoveAll(entry =>
            {
                if (!_entriesDictionary.ContainsKey(entry.Id)) 
                    return true; // purge existing ghosts
                
                bool isAfterMin = !query.MinTime.HasValue || entry.StartTime >= query.MinTime.Value;
                bool isBeforeMax = !query.MaxTime.HasValue || entry.StartTime <= query.MaxTime.Value;
                bool isClassMatch = query.Classes == null || query.Classes.Contains(entry.Classification);

                if (isAfterMin && isBeforeMax && isClassMatch)
                {
                    _entriesDictionary.Remove(entry.Id); 
                    return true; // purge from list
                }

                return false;
            });
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ResultEntry[]> GetAsync(string[] resultIds)
    {
        if (resultIds == null) throw new ArgumentNullException(nameof(resultIds));
        ThrowIfDisposed();

        _lock.EnterReadLock();
        try
        {
            var entries = new List<ResultEntry>(resultIds.Length);
            foreach (var id in resultIds)
            {
                if (_entriesDictionary.TryGetValue(id, out var entry))
                {
                    entries.Add(entry);
                }
            }
            return Task.FromResult(entries.ToArray());
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public Task<ResultEntry?> GetLatestAsync()
    {
        ThrowIfDisposed();

        _lock.EnterReadLock();
        try
        {
            // backtrack through the append-only log to bypass ghost entries
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entriesDictionary.ContainsKey(_entries[i].Id))
                {
                    return Task.FromResult<ResultEntry?>(_entries[i]);
                }
            }
            return Task.FromResult<ResultEntry?>(null);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(string[] resultIds)
    {
        if (resultIds == null) throw new ArgumentNullException(nameof(resultIds));
        ThrowIfDisposed();

        _lock.EnterWriteLock();
        try
        {
            // avoids an O(N) List.Remove traversal. Leaves "ghosts" in the list 
            // that are safely ignored by queries and purged during the next QueryDeleteAsync.
            foreach (var id in resultIds)
            {
                _entriesDictionary.Remove(id);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync()
    {
        ThrowIfDisposed();

        _lock.EnterWriteLock();
        try
        {
            _entries.Clear();
            _entriesDictionary.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> GetCountAsync()
    {
        ThrowIfDisposed();

        _lock.EnterReadLock();
        try
        {
            // thread-safe read
            return Task.FromResult(_entriesDictionary.Count);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
    
    private int GetLowerBoundIndex(StreamTime minTime)
    {
        int left = 0;
        int right = _entries.Count - 1;
        int resultIndex = _entries.Count;

        while (left <= right)
        {
            int mid = left + (right - left) / 2;

            if (_entries[mid].StartTime >= minTime)
            {
                resultIndex = mid;
                right = mid - 1;
            }
            else
            {
                left = mid + 1;
            }
        }

        return resultIndex;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(InMemoryResultRepository));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _lock.Dispose();
    }
}