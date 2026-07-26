using System.Net.Sockets;
using Maldact.Core.Data;
using Maldact.Core.ML;
using Maldact.Core.ML.Inference;
using Maldact.Core.Preprocessing;
using Maldact.Core.Results;
using Maldact.Core.Server;
using Maldact.Core.Server.Authentication;

namespace Maldact.Backend.Server.Streaming;

/// <summary>
/// Securely consumes a raw byte stream, decodes the payload directly into zero-allocation memory pools, 
/// and reliably routes the structured chunks through the machine learning inference pipeline.
/// </summary>
internal class StreamingReceiver
{
    private const int ChunkSize = 64;
    
    private readonly Stream _stream;
    private readonly IInferenceEngine _engine;
    private readonly IResultRepository _resultRepository;
    private readonly int _inputDimension;
    
    private int _isRunning;

    /// <summary>
    /// Gets a value indicating whether the receiver is currently processing a stream.
    /// </summary>
    public bool Running => _isRunning == 1;

    /// <summary>
    /// Initializes a new instance of the StreamingReceiver.
    /// </summary>
    /// <param name="stream">The underlying network or file stream to read from.</param>
    /// <param name="engine">The inference engine used to classify decoded data chunks.</param>
    /// <param name="resultRepository">The storage boundary for persisting confirmed predictions.</param>
    /// <exception cref="ArgumentNullException">Thrown if any required dependency is null.</exception>
    public StreamingReceiver(
        Stream stream,
        IInferenceEngine engine,
        IResultRepository resultRepository)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _resultRepository = resultRepository ?? throw new ArgumentNullException(nameof(resultRepository));
        _inputDimension = engine.InputDimension;
    }

    /// <summary>
    /// Starts the continuous stream reading and inference loop safely managing memory pooling and stream termination.
    /// </summary>
    /// <param name="ct">The token to monitor for cancellation requests.</param>
    /// <exception cref="InvalidOperationException">Thrown if the receiver is already running.</exception>
    public async Task RunAsync(CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            throw new InvalidOperationException("Only one stream allowed per instance!");
        }
        
        try
        {
            int bytesPerFrame = _inputDimension * sizeof(float);
            int bytesPerChunk = ChunkSize * bytesPerFrame;
            
            byte[] accumulationBuffer = new byte[128 * 1024]; 
            int bufferedBytes = 0;

            Task<ResultEntry[]>? previousInferenceTask = null;
            ResultEntry[]? previousResults = null;

            while (!ct.IsCancellationRequested)
            {
                int spaceAvailable = accumulationBuffer.Length - bufferedBytes;
                int read = await _stream.ReadAsync(accumulationBuffer.AsMemory(bufferedBytes, spaceAvailable), ct);
                
                if (read == 0) break; // Clean EOF
                
                bufferedBytes += read;

                while (bufferedBytes >= bytesPerChunk)
                {
                    
                    var chunk = PipelineChunk.Rent(ChunkSize, _inputDimension);
                    chunk.DecodeRawNetworkBytes(accumulationBuffer, 0, bytesPerChunk);
                    
                    if (previousInferenceTask != null)
                    {
                        previousResults = await previousInferenceTask; // await previous
                    }
                    previousInferenceTask = ProcessAndDisposeChunkAsync(chunk, ct);  // immediately launch new
                    
                    if (previousResults != null && previousResults.Length > 0)
                    {
                        await _resultRepository.SaveAsync(previousResults);
                    }
                    
                    bufferedBytes -= bytesPerChunk;
                    
                    if (bufferedBytes > 0)
                    {
                        Buffer.BlockCopy(accumulationBuffer, bytesPerChunk, accumulationBuffer, 0, bufferedBytes);
                    }
                }
            }

            if (previousInferenceTask != null)
            {
                var results = await previousInferenceTask;
                if (results != null && results.Length > 0)
                {
                    await _resultRepository.SaveAsync(results);
                }
            }

            // flush any remaining partial chunk (the tail)
            if (bufferedBytes >= bytesPerFrame)
            {
                int remainingFrames = bufferedBytes / bytesPerFrame;
                var finalChunk = PipelineChunk.Rent(remainingFrames, _inputDimension);
                finalChunk.DecodeRawNetworkBytes(accumulationBuffer, 0, remainingFrames * bytesPerFrame);

                // await directly since there are no more network reads
                var finalResults = await ProcessAndDisposeChunkAsync(finalChunk, ct);
                if (finalResults != null && finalResults.Length > 0)
                {
                    await _resultRepository.SaveAsync(finalResults);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // cleanly ignore expected task cancellations
        }
        finally
        {
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }

    /// <summary>
    /// Evaluates the chunk and guarantees disposal.
    /// Utilizes a synchronous fast-path to eliminate async state machine allocations 
    /// under high-throughput conditions where the inference engine completes synchronously.
    /// </summary>
    private Task<ResultEntry[]> ProcessAndDisposeChunkAsync(PipelineChunk chunk, CancellationToken ct)
    {
        var task = _engine.ClassifyAsync(chunk, ct);
        
        if (task.IsCompletedSuccessfully)
        {
            chunk.Dispose();
            return task;
        }
        
        return AwaitAndDisposeAsync(task, chunk);
    }
    
    // fallback state machine for true asynchronous delays
    private async Task<ResultEntry[]> AwaitAndDisposeAsync(Task<ResultEntry[]> pendingTask, PipelineChunk chunk)
    {
        try
        {
            return await pendingTask;
        }
        finally
        {
            chunk.Dispose();
        }
    }
}