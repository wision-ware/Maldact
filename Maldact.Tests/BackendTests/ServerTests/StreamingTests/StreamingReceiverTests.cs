using FluentAssertions;
using Maldact.Backend.Server.Streaming;
using Maldact.Core.Data;
using Maldact.Core.ML;
using Maldact.Core.ML.Inference;
using Maldact.Core.Results;

namespace Maldact.Tests.BackendTests.ServerTests.StreamingTests;

/// <summary>
/// Verifies the thread-safety, zero-allocation byte decoding, and termination boundaries of the streaming ingestion pipeline.
/// </summary>
public class StreamingReceiverTests
{
    private const int ReceiverChunkSize = 64; // hardcoded in the receiver
    private const int MockInputDimension = 10;
    
    [Fact]
    public void Constructor_NullDependencies_ThrowsArgumentNullException()
    {
        // arrange
        using var stream = new MemoryStream();
        var engine = new StubInferenceEngine();
        var repo = new StubResultRepository();

        // act & assert
        FluentActions.Invoking(() => new StreamingReceiver(null!, engine, repo)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new StreamingReceiver(stream, null!, repo)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new StreamingReceiver(stream, engine, null!)).Should().Throw<ArgumentNullException>();
    }
    
    [Fact]
    public async Task RunAsync_ConcurrentInvocations_ThrowsInvalidOperationException()
    {
        // arrange
        using var tcs = new CancellationTokenSource();
        using var stream = new HangingStream(); // explicitly hangs to keep the lock active
        var receiver = new StreamingReceiver(stream, new StubInferenceEngine(), new StubResultRepository());

        // act: fire without awaiting to lock the state on the hanging stream
        var backgroundTask = receiver.RunAsync(tcs.Token);

        // assert: immediate secondary invocation must violently fail
        await FluentActions.Invoking(() => receiver.RunAsync(CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only one stream allowed*");

        // cleanup
        await tcs.CancelAsync();
        
        // suppress the task cancellation exception for test clean up
        try { await backgroundTask; } catch (OperationCanceledException) { }
    }
    
    [Fact]
    public async Task RunAsync_EndOfStream_TerminatesCleanly()
    {
        // arrange
        var repo = new StubResultRepository();
        var exactChunkBytes = new byte[MockInputDimension * ReceiverChunkSize * sizeof(float)];
        
        using var stream = new MemoryStream(exactChunkBytes);
        var receiver = new StreamingReceiver(stream, new StubInferenceEngine(), repo);

        // act
        await receiver.RunAsync(CancellationToken.None);

        // assert
        receiver.Running.Should().BeFalse("the loop must release its atomic lock upon clean exit.");
        repo.SavedBatches.Should().HaveCount(1, "one exact chunk was processed before the stream ended.");
    }
    
    [Fact]
    public async Task RunAsync_DecodesBytePayloadsCorrectly()
    {
        // arrange
        var engine = new StubInferenceEngine();
        var payload = GenerateDeterministicChunk();
        
        using var stream = new MemoryStream(payload);
        var receiver = new StreamingReceiver(stream, engine, new StubResultRepository());

        // act
        await receiver.RunAsync(CancellationToken.None);

        // assert
        engine.CapturedChunks.Should().HaveCount(1);
        var decodedChunk = engine.CapturedChunks[0];

        // The chunk should be exactly size: Frames * Features
        decodedChunk.Length.Should().Be(ReceiverChunkSize * MockInputDimension);
        
        // verify specific deterministic indices survived the direct unmanaged BlockCopy
        decodedChunk[0].Should().Be(1.0f); // frame 0, feature 0 -> index 0
        
        // frame 1, feature 5 -> index (1 * 10) + 5 = 15
        decodedChunk[(1 * MockInputDimension) + 5].Should().Be(16.0f); 
    }
    
    /// <summary>
    /// Generates a perfectly sized byte array packed with predictable float values for memory verification.
    /// </summary>
    private static byte[] GenerateDeterministicChunk()
    {
        var buffer = new byte[ReceiverChunkSize * MockInputDimension * sizeof(float)];
        
        // simulate the client encoding float features
        for (int frame = 0; frame < ReceiverChunkSize; frame++)
        {
            for (int feature = 0; feature < MockInputDimension; feature++)
            {
                // unique float per feature slot shifted by 1 so 0 index doesn't yield 0
                // (e.g., frame 1 feature 5 = 10 + 5 + 1 = 16.0f)
                float value = (frame * 10) + feature + 1.0f;
                var bytes = BitConverter.GetBytes(value);
                
                int offset = (frame * MockInputDimension * sizeof(float)) + (feature * sizeof(float));
                Buffer.BlockCopy(bytes, 0, buffer, offset, sizeof(float));
            }
        }

        return buffer;
    }

    /// <summary>
    /// A localized stream stub that simulates a hanging network socket by infinitely awaiting reads 
    /// until a cancellation token triggers.
    /// </summary>
    private class HangingStream : MemoryStream
    {
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Task.Delay(-1, cancellationToken);
            return 0;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(-1, cancellationToken);
            return 0;
        }
    }

    /// <summary>
    /// Lightweight interception stub bypassing heavy ML library dependencies.
    /// Updated to support zero-allocation PipelineChunk architecture.
    /// </summary>
    private class StubInferenceEngine : IInferenceEngine
    {
        public int InputDimension => MockInputDimension;
        
        // Flat array captures since the pipeline chunk flattens dimensionality
        public List<float[]> CapturedChunks { get; } = new();

        public Task<ResultEntry[]> ClassifyAsync(PipelineChunk chunk, CancellationToken ct = default)
        {
            // CRITICAL: We must copy the data to the managed heap here because the chunk's 
            // internal array will be immediately returned to the ArrayPool when this method exits.
            CapturedChunks.Add(chunk.CurrentData.ToArray());
            
            // yield a dummy result to trigger the repository save branch
            var mockEntry = new ResultEntry(
                new ClassificationClass("Test"), 
                new StreamTime(TimeSpan.Zero), 
                new StreamTime(TimeSpan.Zero), 
                new StreamTime(TimeSpan.Zero), 
                0.9f, 
                Guid.NewGuid().ToString());

            return Task.FromResult(new[] { mockEntry });
        }

        public void Dispose() { }
    }

    /// <summary>
    /// Localized trace stub to verify persistence routing.
    /// </summary>
    private class StubResultRepository : IResultRepository
    {
        public List<ResultEntry[]> SavedBatches { get; } = new();

        public Task SaveAsync(ResultEntry[] results)
        {
            SavedBatches.Add(results);
            return Task.CompletedTask;
        }

        // bypass all other interface methods (not invoked by the ingestion loop)
        public Task<ResultEntry[]> GetAsync(string[] resultIds) => throw new NotImplementedException();
        public Task<ResultEntry?> GetLatestAsync() => throw new NotImplementedException();
        public Task DeleteAsync(string[] resultIds) => throw new NotImplementedException();
        public Task<IEnumerable<ResultEntry>> QueryAsync(ResultQuery query) => throw new NotImplementedException();
        public Task QueryDeleteAsync(ResultQuery query) => throw new NotImplementedException();
        public Task ClearAsync() => throw new NotImplementedException();
        public Task<int> GetCountAsync() => throw new NotImplementedException();
    }
}