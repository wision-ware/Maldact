using System.Buffers;
using Maldact.Core.Results;
using Microsoft.Extensions.ObjectPool;

namespace Maldact.Core.Data;

/// <summary>
/// Represents the strict lifecycle states of a data chunk as it moves through the processing pipeline.
/// </summary>
public enum ChunkState
{
    Initial,
    Preprocessing,
    Inferred,
    Consolidated,
    Disposed
}

/// <summary>
/// A state-machine driven data carrier that manages zero-allocation memory pooling.
/// Safely transitions data through preprocessing, inference, and consolidation without leaking unmanaged arrays.
/// </summary>
public sealed class PipelineChunk : IDisposable
{
    private float[] _activeBuffer;
    private int _activeLength;
    private ResultEntry[] _finalResults;
    
    private static readonly ObjectPool<PipelineChunk> _chunkPool = 
        new DefaultObjectPool<PipelineChunk>(new DefaultPooledObjectPolicy<PipelineChunk>());

    /// <summary>
    /// Gets the current lifecycle state of the pipeline chunk.
    /// </summary>
    public ChunkState State { get; private set; }

    /// <summary>
    /// Safely exposes the current valid data to the caller without exposing the underlying pooled array.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the chunk has already been disposed.</exception>
    public ReadOnlySpan<float> CurrentData => 
        State != ChunkState.Disposed 
            ? _activeBuffer.AsSpan(0, _activeLength) 
            : throw new ObjectDisposedException(nameof(PipelineChunk));

    // The required parameterless constructor for the ObjectPool
    public PipelineChunk() { }
    
    // The new initialization logic that resets a recycled chunk
    public void Initialize(int frames, int featureDim)
    {
        if (frames <= 0) throw new ArgumentOutOfRangeException(nameof(frames));
        if (featureDim <= 0) throw new ArgumentOutOfRangeException(nameof(featureDim));

        _activeLength = frames * featureDim;
        _activeBuffer = ArrayPool<float>.Shared.Rent(_activeLength);
        State = ChunkState.Initial;
        _finalResults = null; // Clear any residual references to allow GC
    }

    // The factory method
    public static PipelineChunk Rent(int frames, int featureDim)
    {
        var chunk = _chunkPool.Get();
        chunk.Initialize(frames, featureDim);
        return chunk;
    }

    /// <summary>
    /// Initializes a new pipeline chunk by flattening a legacy jagged array into a rented pool buffer.
    /// Maintained for backwards compatibility with local batch testing and dataset creation.
    /// </summary>
    /// <param name="jaggedInput">The initial multi-dimensional sensor data to be processed.</param>
    /// <param name="featureDim">The number of features per temporal frame.</param>
    /// <exception cref="ArgumentNullException">Thrown if the input array is null.</exception>
    public PipelineChunk(float[][] jaggedInput, int featureDim)
    {
        if (jaggedInput == null) throw new ArgumentNullException(nameof(jaggedInput));

        int requiredLength = jaggedInput.Length * featureDim;
        _activeBuffer = ArrayPool<float>.Shared.Rent(requiredLength);
        _activeLength = requiredLength;
        
        for (int i = 0; i < jaggedInput.Length; i++)
        {
            Array.Copy(jaggedInput[i], 0, _activeBuffer, i * featureDim, featureDim);
        }
        
        State = ChunkState.Initial;
    }

    /// <summary>
    /// Decodes raw network bytes directly into the rented float buffer, bypassing intermediate array allocations.
    /// </summary>
    /// <param name="sourceBuffer">The raw byte buffer from the network stream.</param>
    /// <param name="sourceOffset">The starting byte index in the source buffer.</param>
    /// <param name="bytesToDecode">The total number of bytes to copy.</param>
    /// <exception cref="InvalidOperationException">Thrown if called on a chunk that is not in the Initial state.</exception>
    /// <exception cref="ArgumentNullException">Thrown if the source buffer is null.</exception>
    public void DecodeRawNetworkBytes(byte[] sourceBuffer, int sourceOffset, int bytesToDecode)
    {
        if (State != ChunkState.Initial) throw new InvalidOperationException("Can only decode into an uninitialized initial chunk.");
        if (sourceBuffer == null) throw new ArgumentNullException(nameof(sourceBuffer));
        
        // Directly copy bytes into the floating-point memory space safely
        Buffer.BlockCopy(sourceBuffer, sourceOffset, _activeBuffer, 0, bytesToDecode);
    }

    /// <summary>
    /// Advances the pipeline step. Rents a new array for the output, and safely returns the old one.
    /// </summary>
    /// <param name="requiredLength">The size of the contiguous array needed for the next processing step.</param>
    /// <returns>A writable Span for the preprocessor to dump its results into.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the chunk has already been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if called after the chunk has reached the inference stage.</exception>
    public Span<float> AdvancePreprocessingStep(int requiredLength)
    {
        if (State == ChunkState.Disposed) throw new ObjectDisposedException(nameof(PipelineChunk));
        if (State >= ChunkState.Inferred) throw new InvalidOperationException("Cannot preprocess after inference.");
        
        State = ChunkState.Preprocessing;
        return AdvanceBuffer(requiredLength);
    }

    /// <summary>
    /// Transitions the chunk from preprocessing to inference, preparing the buffer for raw probabilities.
    /// </summary>
    /// <param name="requiredLength">The required size of the probability output buffer.</param>
    /// <returns>A writable Span for the machine learning engine to dump its probabilities into.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the chunk has already been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the chunk has already been transitioned to inference.</exception>
    public Span<float> TransitionToInference(int requiredLength)
    {
        if (State == ChunkState.Disposed) throw new ObjectDisposedException(nameof(PipelineChunk));
        if (State >= ChunkState.Inferred) throw new InvalidOperationException("Chunk has already been inferred.");

        State = ChunkState.Inferred;
        return AdvanceBuffer(requiredLength);
    }

    /// <summary>
    /// Consolidates the chunk. Saves the final discrete events and frees the unmanaged float array back to the system pool.
    /// </summary>
    /// <param name="results">The discrete, consolidated classification events.</param>
    /// <exception cref="InvalidOperationException">Thrown if attempting to consolidate a chunk that has not yet passed through inference.</exception>
    public void TransitionToConsolidated(ResultEntry[] results)
    {
        if (State != ChunkState.Inferred) throw new InvalidOperationException("Can only consolidate inferred chunks.");

        State = ChunkState.Consolidated;
        _finalResults = results ?? throw new ArgumentNullException(nameof(results));

        if (_activeBuffer != null)
        {
            ArrayPool<float>.Shared.Return(_activeBuffer);
            _activeBuffer = null;
        }
    }

    /// <summary>
    /// Yields the final result entries for insertion into the Result Repository.
    /// </summary>
    /// <returns>An array of consolidated result entries.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the chunk is not fully consolidated.</exception>
    public ResultEntry[] YieldFinal()
    {
        if (State != ChunkState.Consolidated) throw new InvalidOperationException("Chunk is not fully consolidated.");
        return _finalResults;
    }

    /// <summary>
    /// Internal helper to swap pooled buffers securely without leaking memory.
    /// </summary>
    private Span<float> AdvanceBuffer(int requiredLength)
    {
        float[] nextBuffer = ArrayPool<float>.Shared.Rent(requiredLength);

        if (_activeBuffer != null)
        {
            ArrayPool<float>.Shared.Return(_activeBuffer);
        }

        _activeBuffer = nextBuffer;
        _activeLength = requiredLength;

        return _activeBuffer.AsSpan(0, _activeLength);
    }
    
    /// <summary>
    /// BACKDOOR: Bypasses the standard pipeline lifecycle to explicitly inject pre-computed inference data from disk.
    /// Exclusively used by the Consolidator Tuning Loop to evaluate cached predictions without re-running ML models.
    /// </summary>
    /// <param name="sourceBuffer">The raw byte buffer loaded from disk.</param>
    /// <param name="sourceOffset">The starting byte index in the source buffer.</param>
    /// <param name="bytesToDecode">The total number of bytes to copy.</param>
    public void LoadInferredStateFromBytes(byte[] sourceBuffer, int sourceOffset, int bytesToDecode)
    {
        if (State != ChunkState.Initial) throw new InvalidOperationException("Can only load inferred state into an uninitialized chunk.");
        if (sourceBuffer == null) throw new ArgumentNullException(nameof(sourceBuffer));
            
        State = ChunkState.Inferred;
        Buffer.BlockCopy(sourceBuffer, sourceOffset, _activeBuffer, 0, bytesToDecode);
    }

    /// <summary>
    /// The ultimate fail-safe. Guarantees the array is returned to the pool even if a CancellationToken aborts the pipeline.
    /// </summary>
    public void Dispose()
    {
        if (State == ChunkState.Disposed)
            return;
        
        if (_activeBuffer != null)
        {
            ArrayPool<float>.Shared.Return(_activeBuffer);
            _activeBuffer = null;
        }
        
        State = ChunkState.Disposed;
        _chunkPool.Return(this);
    }
}