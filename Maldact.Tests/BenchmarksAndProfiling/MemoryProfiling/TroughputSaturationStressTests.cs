using Maldact.Backend.ML.Consolidation;
using Maldact.Backend.ML.Inference;
using Maldact.Backend.ML.Inference.OverlapMerging;
using Maldact.Backend.ML.Modules;
using Maldact.Backend.Preprocessing.Pipelines;
using Maldact.Backend.Server.Streaming;
using Maldact.Core.Data;
using Maldact.Core.ML;
using Maldact.Core.ML.Inference;
using Maldact.Core.Preprocessing;
using Maldact.Core.Results;
using Microsoft.ML;
using TorchSharp;

namespace Maldact.Tests.BenchmarksAndProfiling.MemoryProfiling;

public class ThroughputSaturationStressTests
{
    private const int FeatureDimension = 10;
    private const int NumClasses = 2;
    private const double SampleRateHz = 100.0;

    // Shared state to aggregate throughput across N concurrent threads
    public class SharedTelemetryState
    {
        public long GlobalChunksProcessed;
    }

    private class GlobalProfilingProxy : IRawInferenceEngine
    {
        private readonly IRawInferenceEngine _inner;
        private readonly SharedTelemetryState _state;

        public GlobalProfilingProxy(IRawInferenceEngine inner, SharedTelemetryState state)
        {
            _inner = inner;
            _state = state;
        }

        public void Classify(PipelineChunk chunk)
        {
            // Track the aggregate throughput across all active streams
            Interlocked.Increment(ref _state.GlobalChunksProcessed);
            _inner.Classify(chunk);
        }
        public void Dispose() => _inner.Dispose();
    }

    [Fact]
    [Trait("Category", "SaturationStress")]
    public async Task Saturation_MLNET()
    {
        var concurrencyLevels = new[] { 1, 2, 4, 8, 16 };

        foreach (int concurrencyLevel in concurrencyLevels)
        {
            var sharedState = new SharedTelemetryState();
            var receivers = new List<StreamingReceiver>();
            var mlContext = new MLContext();
            var dummyData = mlContext.Data.LoadFromEnumerable(new List<TreeDataRow>());
            
            var mlPipeline = mlContext.Transforms.CustomMapping(
                (TreeDataRow input, TreePrediction output) => { output.Probabilities = new float[NumClasses]; }, 
                contractName: "DummyMapping");
            var transformer = mlPipeline.Fit(dummyData);

            // Build N completely independent streams to mimic the StreamingManager
            for (int i = 0; i < concurrencyLevel; i++)
            {
                var emptyPipeline = new LinearPipelineCompositePreprocessor(new IDataPreprocessor[] { new PassThroughStubPreprocessor(FeatureDimension) });
                var realConsolidator = new SlidingWindowHysteresisResultConsolidator(
                    SampleRateHz, new StreamTime(TimeSpan.Zero), TimeSpan.FromSeconds(1), 
                    new[] { new ClassificationClass("Idle"), new ClassificationClass("Active") }, 0.8f, 0.2f);

                var predictionEngine = mlContext.Model.CreatePredictionEngine<TreeDataRow, TreePrediction>(transformer);
                var mlNetEngine = new TreeInferenceEngine(predictionEngine, 10, FeatureDimension, 2, NumClasses);
                
                var trackingProxy = new GlobalProfilingProxy(mlNetEngine, sharedState);
                var orchestrator = new InferenceEngine(trackingProxy, realConsolidator, emptyPipeline);
                
                var receiver = new StreamingReceiver(new InfiniteDummyStream(), orchestrator, new StubResultRepository());
                receivers.Add(receiver);
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var csvLines = RunGlobalLogger(sharedState, out var timer);

            // Unleash all streams simultaneously
            await Task.WhenAll(receivers.Select(r => r.RunAsync(ct: cts.Token)));
            timer.Change(Timeout.Infinite, Timeout.Infinite);

            ExportCsv($"Saturation_MLNET_{concurrencyLevel}_Streams.csv", csvLines);

            // 10-second thermal cooldown and OS scheduler flush
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    [Trait("Category", "SaturationStress")]
    public async Task Saturation_Torch()
    {
        var concurrencyLevels = new[] { 1, 2, 4, 8, 16 };

        foreach (int concurrencyLevel in concurrencyLevels)
        {
            var sharedState = new SharedTelemetryState();
            var receivers = new List<StreamingReceiver>();

            for (int i = 0; i < concurrencyLevel; i++)
            {
                var emptyPipeline = new LinearPipelineCompositePreprocessor(new IDataPreprocessor[] { new PassThroughStubPreprocessor(FeatureDimension) });
                var realConsolidator = new SlidingWindowHysteresisResultConsolidator(
                    SampleRateHz, new StreamTime(TimeSpan.Zero), TimeSpan.FromSeconds(1), 
                    new[] { new ClassificationClass("Idle"), new ClassificationClass("Active") }, 0.8f, 0.2f);

                var cnnModel = new TimeSeriesCnn("ProfilingCnn", FeatureDimension, new[] { 16, 32 }, 3, NumClasses);
                var torchEngine = new TorchInferenceEngine<MaxMerger>(
                    cnnModel, torch.CPU, seqLength: 10, featureDim: FeatureDimension, stride: 2, numClasses: NumClasses);
                
                var trackingProxy = new GlobalProfilingProxy(torchEngine, sharedState);
                var orchestrator = new InferenceEngine(trackingProxy, realConsolidator, emptyPipeline);
                
                var receiver = new StreamingReceiver(new InfiniteDummyStream(), orchestrator, new StubResultRepository());
                receivers.Add(receiver);
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var csvLines = RunGlobalLogger(sharedState, out var timer);

            await Task.WhenAll(receivers.Select(r => r.RunAsync(ct: cts.Token)));
            timer.Change(Timeout.Infinite, Timeout.Infinite);

            ExportCsv($"Saturation_Torch_{concurrencyLevel}_Streams.csv", csvLines);

            // 10-second thermal cooldown and OS scheduler flush
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    private List<string> RunGlobalLogger(SharedTelemetryState state, out Timer timer)
    {
        var csvLines = new List<string> { "Time(Seconds),TotalChunks,CpuUsage(%)" };
        var startTime = DateTime.UtcNow;

        var process = System.Diagnostics.Process.GetCurrentProcess();
        var lastCpuTime = process.TotalProcessorTime;
        var lastWallTime = DateTime.UtcNow;
        int processorCount = Environment.ProcessorCount;

        timer = new Timer(_ =>
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - startTime).TotalSeconds;
            if (elapsed > 60.5) return;

            var currentCpuTime = process.TotalProcessorTime;
            var cpuTimeDelta = (currentCpuTime - lastCpuTime).TotalMilliseconds;
            var wallTimeDelta = (now - lastWallTime).TotalMilliseconds;
            
            double cpuUsage = 0;
            if (wallTimeDelta > 0)
            {
                cpuUsage = Math.Clamp((cpuTimeDelta / (wallTimeDelta * processorCount)) * 100.0, 0, 100);
            }
            
            lastCpuTime = currentCpuTime;
            lastWallTime = now;
            long currentChunks = Interlocked.Read(ref state.GlobalChunksProcessed);

            lock (csvLines)
            {
                csvLines.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"{Math.Min(60.0, Math.Round(elapsed, 1))},{currentChunks},{cpuUsage:F2}"));
            }
        }, null, 0, 100);

        return csvLines;
    }

    private void ExportCsv(string fileName, List<string> lines)
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var exportPath = Path.Combine(desktopPath, fileName);
        File.WriteAllLines(exportPath, lines);
    }

    private class InfiniteDummyStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            buffer.Span.Clear(); 
            return new ValueTask<int>(buffer.Length);
        }
    }
    
    private class StubResultRepository : IResultRepository
    {
        private readonly Task _completedTask = Task.CompletedTask;
        
        public Task SaveAsync(ResultEntry[] results)
        {
            return _completedTask;
        }

        public Task<ResultEntry[]> GetAsync(string[] resultIds) => throw new NotImplementedException();
        public Task<ResultEntry?> GetLatestAsync() => throw new NotImplementedException();
        public Task DeleteAsync(string[] resultIds) => throw new NotImplementedException();
        public Task<IEnumerable<ResultEntry>> QueryAsync(ResultQuery query) => throw new NotImplementedException();
        public Task QueryDeleteAsync(ResultQuery query) => throw new NotImplementedException();
        public Task ClearAsync() => throw new NotImplementedException();
        public Task<int> GetCountAsync() => throw new NotImplementedException();
    }
    
    private class PassThroughStubPreprocessor : IDataPreprocessor
    {
        public int InputDimension { get; }
        public int OutputDimension { get; }

        public PassThroughStubPreprocessor(int dimension)
        {
            InputDimension = OutputDimension = dimension;
        }

        public void Process(PipelineChunk chunk)
        {
            ReadOnlySpan<float> input = chunk.CurrentData;
            if (input.Length == 0) return;
            
            Span<float> output = chunk.AdvancePreprocessingStep(input.Length);
            input.CopyTo(output);
        }
    }
}