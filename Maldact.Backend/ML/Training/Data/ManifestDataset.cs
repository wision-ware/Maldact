using System.Buffers;
using System.Runtime.InteropServices;
using System.Text.Json;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.Data;
using Maldact.Core.ML;
using Maldact.Core.ML.Training;

namespace Maldact.Backend.ML.Training.Data;

/// <summary>
/// The core data loading engine. Maps JSON dataset manifests to physical binary files 
/// and marshals byte streams into ML-ready floating-point feature tensors.
/// </summary>
public class ManifestDataset : IDisposable, IRawContinuousLabeledDataLoader, IRawLabeledTrainingDataLoader
{
    private const int BufferSize = 4096;
    
    private readonly string _datasetDirectory;
    private readonly DatasetManifest _manifest;
    
    private readonly Dictionary<string, FileStream> _openTrainingStreams = new();
    private readonly Dictionary<string, FileStream> _openCrossValidationStreams = new();
    private readonly Dictionary<string, List<AbsoluteEvent>> _parsedEvents = new();
    
    private readonly Random _rng;

    /// <inheritdoc />
    public double SampleRateHz => _manifest.GlobalSampleRateHz;

    /// <inheritdoc />
    public ClassificationClass[] Classes { get; }

    /// <summary>
    /// The mathematical signal processing pipeline applied to the dataset prior to tensor compilation.
    /// </summary>
    public PreprocessingContract? Preprocessing => _manifest.Preprocessing;

    private ManifestDataset(string datasetDirectory, DatasetManifest manifest, int? rngSeed)
    {
        _datasetDirectory = datasetDirectory;
        _manifest = manifest;
        Classes = manifest.Classes.Select(name => new ClassificationClass(name)).ToArray();
        _rng = new Random(rngSeed ?? Environment.TickCount);
    }

    /// <summary>
    /// Asynchronously loads the dataset manifest from disk, validates its structural integrity, 
    /// and initializes pointers to the required binary streams.
    /// </summary>
    /// <param name="directoryPath">The absolute or relative path to the directory containing 'dataset.processed.json'.</param>
    /// <param name="rngSeed">An optional seed to ensure deterministic random sampling during training.</param>
    /// <returns>A fully initialized and memory-mapped ManifestDataset instance.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the manifest file or its referenced binaries cannot be located.</exception>
    /// <exception cref="InvalidDataException">Thrown when the manifest JSON is malformed or conceptually invalid.</exception>
    public static async Task<ManifestDataset> LoadAsync(string directoryPath, int? rngSeed)
    {
        // todo implement default name passing to decouple form DatasetBaker
        string manifestPath = Path.Combine(directoryPath, DatasetBaker.BakedManifestName);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"No {DatasetBaker.BakedManifestName} found in {directoryPath}");

        using var jsonStream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync(jsonStream, DatasetJsonContext.Default.DatasetManifest)
                       ?? throw new InvalidDataException("Manifest JSON is empty or invalid");
        
        manifest.Validate(directoryPath);

        var dataset = new ManifestDataset(directoryPath, manifest, rngSeed);
        dataset.InitializeFileStreams();
        
        return dataset;
    }

    /// <summary>
    /// Pre-allocates OS file handles and computes event maps to prevent disk bottlenecks during hot training loops.
    /// </summary>
    private void InitializeFileStreams()
    {
        // cache class definitions for O(1) resolution during event mapping
        var eventLookup = Classes.ToDictionary(c => c.ClassName, c => c);

        InitializeStreamType(_openTrainingStreams, _manifest.TrainingStreams, eventLookup);
        InitializeStreamType(_openCrossValidationStreams, _manifest.CrossValidationStreams, eventLookup);

        void InitializeStreamType(Dictionary<string, FileStream> streams, List<StreamManifest> streamInfos, Dictionary<string, ClassificationClass> lookup)
        {
            foreach (var streamInfo in streamInfos)
            {
                string fullPath = Path.Combine(_datasetDirectory, streamInfo.FileName);

                streams[streamInfo.FileName] = new FileStream(
                    fullPath, 
                    FileMode.Open, 
                    FileAccess.Read, 
                    FileShare.Read, 
                    bufferSize: BufferSize, 
                    useAsync: false);

                _parsedEvents[streamInfo.FileName] = streamInfo.Events
                    .Select(dto => dto.ToAbsoluteEvent(lookup))
                    .ToList();
            }
        }
    }

    /// <inheritdoc />
    public RawTrainingWindow GetRandomTrainingWindow(TimeSpan windowDuration)
    {
        if (!_manifest.TrainingReady) 
            throw new InvalidOperationException("The dataset lacks a preprocessing contract and is not ready for training.");

        int featureDimension = _manifest.Preprocessing?.FinalReshaping!.TargetDimension 
                               ?? throw new InvalidDataException("Dataset preprocessing contract is invalid.");
        
        StreamManifest randomStreamInfo;
        double randomStartMs;

        // lock rng to prevent race conditions during concurrent worker thread sampling
        lock (_rng)
        {
            randomStreamInfo = _manifest.TrainingStreams[_rng.Next(_manifest.TrainingStreams.Count)];
            double maxStartMs = randomStreamInfo.DurationMs - windowDuration.TotalMilliseconds;
            
            if (maxStartMs <= 0) 
                throw new InvalidOperationException($"Stream {randomStreamInfo.FileName} is shorter than the requested window duration!");

            randomStartMs = _rng.NextDouble() * maxStartMs;
        }

        var fileStream = _openTrainingStreams[randomStreamInfo.FileName];
        var streamEvents = _parsedEvents[randomStreamInfo.FileName];

        var startOffset = TimeSpan.FromMilliseconds(randomStartMs);
        var endOffset = startOffset + windowDuration;

        // compute absolute disk offsets
        long startSampleIndex = (long)Math.Round(startOffset.TotalSeconds * SampleRateHz);
        int windowSamples = (int)Math.Round(windowDuration.TotalSeconds * SampleRateHz);
        long byteOffset = startSampleIndex * featureDimension * sizeof(float);

        float[][] features = ReadFeatureWindow(fileStream, byteOffset, windowSamples, featureDimension);
        
        var overlappingEvents = streamEvents.Where(ev => 
            ev.StartOffset < endOffset && ev.EndOffset > startOffset
        ).ToList();

        return new RawTrainingWindow(
            features, 
            overlappingEvents, 
            startOffset, 
            endOffset
        );
    }
    
    /// <inheritdoc />
    public IEnumerable<RawTrainingWindow> GetSequentialWindows(TimeSpan windowDuration, TimeSpan stride)
    {
        int featureDimension = _manifest.Preprocessing?.FinalReshaping!.TargetDimension 
                               ?? throw new InvalidDataException("Dataset preprocessing contract is invalid.");
        
        foreach (var streamInfo in _manifest.CrossValidationStreams)
        {
            var fileStream = _openCrossValidationStreams[streamInfo.FileName];
            var streamEvents = _parsedEvents[streamInfo.FileName];
        
            TimeSpan currentOffset = TimeSpan.Zero;
        
            // slide window across the file until the trailing edge exceeds duration
            while (currentOffset + windowDuration <= TimeSpan.FromMilliseconds(streamInfo.DurationMs))
            {
                long startSampleIndex = (long)Math.Round(currentOffset.TotalSeconds * SampleRateHz);
                int windowSamples = (int)Math.Round(windowDuration.TotalSeconds * SampleRateHz);
                long byteOffset = startSampleIndex * featureDimension * sizeof(float);

                float[][] features = ReadFeatureWindow(fileStream, byteOffset, windowSamples, featureDimension);
        
                var overlappingEvents = streamEvents.Where(ev => 
                    ev.StartOffset < currentOffset + windowDuration && ev.EndOffset > currentOffset
                ).ToList();

                yield return new RawTrainingWindow(
                    features, 
                    overlappingEvents, 
                    currentOffset, 
                    currentOffset + windowDuration
                );
            
                currentOffset += stride;
            }
        }
    } 
    
    /// <inheritdoc />
    IEnumerable<float[][]> IRawContinuousLabeledDataLoader.GetContinuousWindows()
    {
        int featureDim = _manifest.Preprocessing?.FinalReshaping!.TargetDimension 
                         ?? throw new InvalidDataException("Dataset preprocessing contract is invalid.");
        
        int bytesPerFrame = featureDim * sizeof(float);
        
        // rent buffer to eliminate LOH allocations during high-throughput validation
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize * bytesPerFrame);
        try
        {
            foreach (var streamInfo in _manifest.CrossValidationStreams)
            {
                var fileStream = _openCrossValidationStreams[streamInfo.FileName];
                long currentByteOffset = 0;

                int bytesRead;
                while ((bytesRead = RandomAccess.Read(fileStream.SafeFileHandle, buffer, currentByteOffset)) > 0)
                {
                    currentByteOffset += bytesRead;
                    int framesRead = bytesRead / bytesPerFrame;
                    if (framesRead == 0) break; 

                    Span<float> flatFloats = MemoryMarshal.Cast<byte, float>(buffer.AsSpan(0, framesRead * bytesPerFrame));

                    float[][] chunk = new float[framesRead][];
                    for (int i = 0; i < framesRead; i++)
                    {
                        chunk[i] = new float[featureDim];
                        flatFloats.Slice(i * featureDim, featureDim).CopyTo(chunk[i]);
                    }

                    yield return chunk;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
    
    /// <inheritdoc />
    AbsoluteEvent[] IRawContinuousLabeledDataLoader.GetGroundTruthEvents()
    {
        int featureDim = _manifest.Preprocessing?.FinalReshaping!.TargetDimension 
                         ?? throw new InvalidDataException("Dataset preprocessing contract is invalid.");
        
        int bytesPerFrame = featureDim * sizeof(float);

        var absoluteEvents = new List<AbsoluteEvent>();
        TimeSpan cumulativeTime = TimeSpan.Zero;

        foreach (var streamInfo in _manifest.CrossValidationStreams)
        {
            var streamEvents = _parsedEvents[streamInfo.FileName];

            // shift event times to align with the flattened global timeline
            foreach (var ev in streamEvents)
            {
                absoluteEvents.Add(new AbsoluteEvent
                    {
                        Class = ev.Class,
                        StartOffset = ev.StartOffset + cumulativeTime,
                        EndOffset = ev.EndOffset + cumulativeTime,
                    }
                );
            }

            // advance timeline by calculating exact duration from physical bytes
            var fileStream = _openCrossValidationStreams[streamInfo.FileName];
            long totalFrames = fileStream.Length / bytesPerFrame;
            
            double fileElapsedSeconds = totalFrames / SampleRateHz;
            cumulativeTime += TimeSpan.FromSeconds(fileElapsedSeconds);
        }

        return absoluteEvents.ToArray();
    }

    /// <summary>
    /// Thread-safe I/O helper that marshals physical disk bytes into managed floating-point arrays.
    /// </summary>
    /// <param name="fileStream">The target stream to read from.</param>
    /// <param name="byteOffset">The absolute physical byte offset in the file.</param>
    /// <param name="windowSamples">The total number of temporal frames to read.</param>
    /// <param name="featureDimension">The number of features per temporal frame.</param>
    /// <returns>A 2D jagged array representing [TimeSteps][Features].</returns>
    /// <exception cref="EndOfStreamException">Thrown if the file truncates before the requested window length.</exception>
    private float[][] ReadFeatureWindow(FileStream fileStream, long byteOffset, int windowSamples, int featureDimension)
    {
        int bytesToRead = windowSamples * featureDimension * sizeof(float);
        
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bytesToRead);
        try
        {
            // use RandomAccess to prevent thread race conditions over mutable Seek pointers
            int bytesRead = RandomAccess.Read(fileStream.SafeFileHandle, buffer.AsSpan(0, bytesToRead), byteOffset);
            if (bytesRead != bytesToRead)
                throw new EndOfStreamException($"Failed to read full window. Expected {bytesToRead} bytes, got {bytesRead}.");

            Span<float> flatFloats = MemoryMarshal.Cast<byte, float>(buffer.AsSpan(0, bytesToRead));
            
            float[][] features = new float[windowSamples][];
            for (int i = 0; i < windowSamples; i++)
            {
                features[i] = new float[featureDimension];
                flatFloats.Slice(i * featureDimension, featureDimension).CopyTo(features[i]);
            }
            return features;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Releases all underlying OS file handles associated with the parsed binary streams.
    /// </summary>
    public void Dispose()
    {
        foreach (var fs in _openTrainingStreams.Values) fs.Dispose();
        foreach (var fs in _openCrossValidationStreams.Values) fs.Dispose();
        GC.SuppressFinalize(this);
    }
}