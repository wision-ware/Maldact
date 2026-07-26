using FluentAssertions;
using Maldact.Backend.Server.Results;
using Maldact.Core.ML;
using Maldact.Core.Results;

namespace Maldact.Tests.BackendTests.ServerTests.ResultsTests;

/// <summary>
/// Verifies the thread-safety, chronological sorting, and soft-delete index compaction of the in-memory repository.
/// </summary>
public class InMemoryResultRepositoryTests
{

    [Fact]
    public async Task Repository_MethodsAfterDispose_ThrowObjectDisposedException()
    {
        // arrange
        var repo = new InMemoryResultRepository();
        repo.Dispose();

        // act & assert
        await FluentActions.Invoking(() => repo.SaveAsync(Array.Empty<ResultEntry>())).Should().NotThrowAsync("empty arrays bypass locks and return early");
        
        await FluentActions.Invoking(() => repo.GetCountAsync()).Should().ThrowAsync<ObjectDisposedException>();
        await FluentActions.Invoking(() => repo.GetLatestAsync()).Should().ThrowAsync<ObjectDisposedException>();
        await FluentActions.Invoking(() => repo.QueryAsync(new ResultQuery())).Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task SaveAsync_OutOfOrderBatches_ForcesChronologicalSort()
    {
        // arrange
        using var repo = new InMemoryResultRepository();
        
        var batch1 = new[] { CreateEntry("3", 300), CreateEntry("4", 400) };
        var historicalBatch = new[] { CreateEntry("1", 100), CreateEntry("2", 200) };

        // act
        await repo.SaveAsync(batch1);
        await repo.SaveAsync(historicalBatch); // forces full sort

        // assert
        var latest = await repo.GetLatestAsync();
        latest.Should().NotBeNull();
        latest!.Id.Should().Be("4", "the historical batch should have been sorted to the beginning, leaving 4 at the tail.");
        
        var count = await repo.GetCountAsync();
        count.Should().Be(4);
    }

    [Fact]
    public async Task SaveAsync_DuplicateIds_SafelyUpserts()
    {
        // arrange
        using var repo = new InMemoryResultRepository();
        var initial = CreateEntry("dup-1", 100, score: 0.5f);
        var updated = CreateEntry("dup-1", 100, score: 0.9f); // same ID, better score

        // act
        await repo.SaveAsync(new[] { initial });
        await repo.SaveAsync(new[] { updated });

        // assert
        var count = await repo.GetCountAsync();
        count.Should().Be(1);

        var retrieved = await repo.GetAsync(new[] { "dup-1" });
        retrieved.Should().ContainSingle();
        retrieved[0].Score.Should().Be(0.9f, "the dictionary indexer should have cleanly overwritten the existing entry.");
    }

    [Fact]
    public async Task QueryAsync_TemporalAndClassBounds_FiltersCorrectly()
    {
        // arrange
        using var repo = new InMemoryResultRepository();
        await repo.SaveAsync(new[]
        {
            CreateEntry("1", 100, className: "A"),
            CreateEntry("2", 200, className: "B"),
            CreateEntry("3", 300, className: "A"),
            CreateEntry("4", 400, className: "C")
        });

        var query = new ResultQuery
        {
            MinTime = new StreamTime(TimeSpan.FromMilliseconds(150)),
            MaxTime = new StreamTime(TimeSpan.FromMilliseconds(350)),
            Classes = new HashSet<ClassificationClass> { new("A") }
        };

        // act
        var results = (await repo.QueryAsync(query)).ToList();

        // assert
        results.Should().ContainSingle();
        results[0].Id.Should().Be("3", "entry 1 is too early, entry 2 is class B, and entry 4 is too late.");
    }
    
    [Fact]
    public async Task DeleteAsync_CreatesGhostEntries_HandledProperlyByReaders()
    {
        // arrange
        using var repo = new InMemoryResultRepository();
        await repo.SaveAsync(new[]
        {
            CreateEntry("1", 100),
            CreateEntry("2", 200),
            CreateEntry("3", 300)
        });

        // act: delete the tail
        await repo.DeleteAsync(new[] { "3" });

        // assert
        var latest = await repo.GetLatestAsync();
        latest!.Id.Should().Be("2", "GetLatestAsync must backtrack over the ghost entry at the tail.");

        var allRemaining = (await repo.QueryAsync(new ResultQuery())).ToList();
        allRemaining.Should().HaveCount(2);
        allRemaining.Select(r => r.Id).Should().NotContain("3");
    }
    
    [Fact]
    public async Task QueryDeleteAsync_MatchesBounds_PurgesAndCompacts()
    {
        // arrange
        using var repo = new InMemoryResultRepository();
        await repo.SaveAsync(new[]
        {
            CreateEntry("1", 100),
            CreateEntry("2", 200), // to be discrete deleted (ghost)
            CreateEntry("3", 300, className: "Target-X"), // to be query deleted
            CreateEntry("4", 400)
        });

        await repo.DeleteAsync(new[] { "2" });

        var query = new ResultQuery { Classes = new HashSet<ClassificationClass> { new("Target-X") } };

        // act
        await repo.QueryDeleteAsync(query);

        // assert
        var count = await repo.GetCountAsync();
        count.Should().Be(2, "entries 1 and 4 remain.");

        var remaining = (await repo.QueryAsync(new ResultQuery())).ToList();
        remaining.Select(r => r.Id).Should().BeEquivalentTo("1", "4");
    }
    
    [Fact]
    public async Task Concurrency_MixedReadWriteWorkload_MaintainsIntegrity()
    {
        // arrange
        using var repo = new InMemoryResultRepository();
        const int writers = 50;
        const int entriesPerWriter = 100;
        
        var tasks = new List<Task>();

        // act: blast concurrent writes, point reads, and queries across the thread pool
        for (int i = 0; i < writers; i++)
        {
            int writerId = i;
            tasks.Add(Task.Run(async () =>
            {
                var batch = Enumerable.Range(0, entriesPerWriter)
                    .Select(j => CreateEntry($"{writerId}-{j}", writerId * 1000 + j))
                    .ToArray();

                await repo.SaveAsync(batch);
                
                // concurrent read pressure
                _ = await repo.GetCountAsync();
                _ = await repo.GetLatestAsync();
                _ = await repo.GetAsync(new[] { $"{writerId}-0" });
            }));
        }

        await Task.WhenAll(tasks);

        // assert
        var totalCount = await repo.GetCountAsync();
        totalCount.Should().Be(writers * entriesPerWriter, "no writes should be dropped due to race conditions.");
    }

    /// <summary>
    /// Scaffolds a complete ResultEntry using relative StreamTimes for straightforward offset math.
    /// </summary>
    private static ResultEntry CreateEntry(string id, double startMs, double durationMs = 50, string className = "TargetA", float score = 0.9f)
    {
        var start = new StreamTime(TimeSpan.FromMilliseconds(startMs));
        var end = new StreamTime(TimeSpan.FromMilliseconds(startMs + durationMs));
        var centroid = new StreamTime(TimeSpan.FromMilliseconds(startMs + (durationMs / 2.0)));

        return new ResultEntry(
            new ClassificationClass(className),
            start,
            end,
            centroid,
            score,
            id
        );
    }
}