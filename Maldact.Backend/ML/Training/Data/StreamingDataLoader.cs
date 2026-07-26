using Maldact.Core.Data;
using Maldact.Core.ML.Training;

namespace Maldact.Backend.ML.Training.Data;

/// <summary>
/// Orchestrates the retrieval of raw data windows and pipes them through a specified tensor formatter.
/// Leverages concurrent I/O to maximize throughput.
/// </summary>
public class StreamingDataLoader : IFormattedLabeledTrainingDataLoader
{
    private readonly IRawLabeledTrainingDataLoader _dataLoader;
    private readonly ILabeledBatchFormatter _formatter;
    private readonly int _batchSize;
    private readonly int _batchesPerEpoch;
    private readonly TimeSpan _windowDuration;
    private readonly TimeSpan _stride;
    private readonly int? _seed;

    /// <summary>
    /// Initializes a new instance of the StreamingDataLoader.
    /// </summary>
    /// <param name="dataLoader">The underlying raw data engine.</param>
    /// <param name="formatter">The mathematical tensor formatter (e.g., GRU, CNN, Tree).</param>
    /// <param name="batchSize">The number of windows per yielded batch.</param>
    /// <param name="batchesPerEpoch">The number of batches to yield before concluding a training epoch.</param>
    /// <param name="stride">The temporal advancement between sequential validation windows.</param>
    /// <param name="windowDuration">The fixed duration of every loaded window.</param>
    /// <param name="seed">Optional fixed seed to determinize the batch loading.</param>
    public StreamingDataLoader(
        IRawLabeledTrainingDataLoader dataLoader, 
        ILabeledBatchFormatter formatter, 
        int batchSize,
        int batchesPerEpoch,
        TimeSpan stride,
        TimeSpan windowDuration,
        int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(dataLoader);
        ArgumentNullException.ThrowIfNull(formatter);
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");
        if (batchesPerEpoch <= 0) throw new ArgumentOutOfRangeException(nameof(batchesPerEpoch), "Batches per epoch must be greater than zero.");
        if (windowDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(windowDuration), "Window duration must be greater than zero.");

        _dataLoader = dataLoader;
        _formatter = formatter;
        _batchSize = batchSize;
        _batchesPerEpoch = batchesPerEpoch;
        _stride = stride;
        _windowDuration = windowDuration;
        _seed = seed;
    }

    /// <inheritdoc />
    public IEnumerable<FormattedLabeledBatch> GenerateBatches()
    {
        // pre-allocate array to eliminate list resize overhead
        var batchWindows = new RawTrainingWindow[_batchSize];

        for (int i = 0; i < _batchesPerEpoch; i++)
        {
            
            if (_seed is null)  // efficient parallel batch loading with no fixed seed
                Parallel.For(0, _batchSize, b =>
                {
                    batchWindows[b] = _dataLoader.GetRandomTrainingWindow(_windowDuration);
                });
            else  // run sequentially for a fixed seed since CPU scheduling is unpredictable
                for (var b = 0; b < _batchSize; b++)
                {
                    batchWindows[b] = _dataLoader.GetRandomTrainingWindow(_windowDuration);
                }

            yield return _formatter.Format(batchWindows);
        }
    }

    /// <inheritdoc />
    public IEnumerable<FormattedLabeledBatch> GetValidationBatches()
    {
        var currentBatch = new List<RawTrainingWindow>(_batchSize);

        // sequential validation reads must remain ordered
        foreach (var window in _dataLoader.GetSequentialWindows(_windowDuration, _stride))
        {
            currentBatch.Add(window);

            if (currentBatch.Count < _batchSize) continue;
            
            yield return _formatter.Format(currentBatch);
            currentBatch.Clear();
        }
        
        // flush the final partial batch
        if (currentBatch.Count > 0)
        {
            yield return _formatter.Format(currentBatch);
        }
    }
}