using Maldact.Backend.ML.Consolidation;
using Maldact.Backend.ML.Inference;
using Maldact.Backend.ML.Inference.OverlapMerging;
using Maldact.Backend.ML.Modules;
using Maldact.Backend.Preprocessing.DSPs;
using Maldact.Backend.Preprocessing.Pipelines;
using Maldact.Backend.Server.Streaming;
using Maldact.Core.Data;
using Maldact.Core.ML;
using Maldact.Core.ML.Consolidation;
using Maldact.Core.ML.Inference;
using Maldact.Core.Preprocessing;
using Maldact.Core.Results;
using Microsoft.ML;
using TorchSharp;

namespace Maldact.Tests.BenchmarksAndProfiling.MemoryProfiling;

public class PipelineIsolationProfilingTests
{
    private const int FeatureDimension = 10;
    private const int NumClasses = 2;
    private const double SampleRateHz = 100.0;
    private const int MockInputDimension = 10;
    

    [Fact]
    [Trait("Category", "IsolationProfiling")]
    public async Task Profile_0_BareReceiverIngestion()
    {
        var stubRawEngine = new PureStubRawEngine(NumClasses);
        var syncOrchestrator = new SyncStubOrchestrator(stubRawEngine);
        var repo = new StubResultRepository();

        using var infiniteStream = new InfiniteDummyStream();
        var receiver = new StreamingReceiver(infiniteStream, syncOrchestrator, repo);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var csvLines = RunIsolationLogger(stubRawEngine, out var timer);
        await receiver.RunAsync(cts.Token);
        timer.Change(Timeout.Infinite, Timeout.Infinite);

        ExportCsv("Profile_0_BareReceiver.csv", csvLines);
    }

    [Fact]
    [Trait("Category", "IsolationProfiling")]
    public async Task Profile_1_OrchestrationBaseline()
    {
        var emptyPipeline = new LinearPipelineCompositePreprocessor(new IDataPreprocessor[] { new PassThroughStubPreprocessor(FeatureDimension) });
        var stubRawEngine = new PureStubRawEngine(NumClasses);
        var stubConsolidator = new PureStubConsolidator();
        var realEngine = new InferenceEngine(stubRawEngine, stubConsolidator, emptyPipeline);
        var repo = new StubResultRepository();

        using var infiniteStream = new InfiniteDummyStream();
        var receiver = new StreamingReceiver(infiniteStream, realEngine, repo);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var csvLines = RunIsolationLogger(stubRawEngine, out var timer);
        await receiver.RunAsync(cts.Token);
        timer.Change(Timeout.Infinite, Timeout.Infinite);

        ExportCsv("Profile_1_Orchestration.csv", csvLines);
        realEngine.Dispose();
    }

    [Fact]
    [Trait("Category", "IsolationProfiling")]
    public async Task Profile_2_DspPipeline()
    {
        var imputation = new ImputationFilter(ImputationFilter.ImputationMethod.ForwardFill, FeatureDimension);
        var smoother = new MovingAverageSmoother(FeatureDimension, windowSize: 5);
        var realPipeline = new LinearPipelineCompositePreprocessor(new IDataPreprocessor[] { imputation, smoother });

        var stubRawEngine = new PureStubRawEngine(NumClasses);
        var stubConsolidator = new PureStubConsolidator();
        var realEngine = new InferenceEngine(stubRawEngine, stubConsolidator, realPipeline);
        var repo = new StubResultRepository();

        using var infiniteStream = new InfiniteDummyStream();
        var receiver = new StreamingReceiver(infiniteStream, realEngine, repo);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var csvLines = RunIsolationLogger(stubRawEngine, out var timer);
        await receiver.RunAsync(cts.Token);
        timer.Change(Timeout.Infinite, Timeout.Infinite);

        ExportCsv("Profile_2_DSP.csv", csvLines);
        realEngine.Dispose();
    }

    [Fact]
    [Trait("Category", "IsolationProfiling")]
    public async Task Profile_3_ResultConsolidator()
    {
        var emptyPipeline = new LinearPipelineCompositePreprocessor(new IDataPreprocessor[] { new PassThroughStubPreprocessor(FeatureDimension) });
        var stubRawEngine = new PureStubRawEngine(NumClasses);
        
        var classes = new[] { new ClassificationClass("Idle"), new ClassificationClass("Active") };
        var realConsolidator = new SlidingWindowHysteresisResultConsolidator(
            SampleRateHz, new StreamTime(TimeSpan.Zero), TimeSpan.FromSeconds(1), classes, 0.8f, 0.2f);
            
        var realEngine = new InferenceEngine(stubRawEngine, realConsolidator, emptyPipeline);
        var repo = new StubResultRepository();

        using var infiniteStream = new InfiniteDummyStream();
        var receiver = new StreamingReceiver(infiniteStream, realEngine, repo);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var csvLines = RunIsolationLogger(stubRawEngine, out var timer);
        await receiver.RunAsync(cts.Token);
        timer.Change(Timeout.Infinite, Timeout.Infinite);

        ExportCsv("Profile_3_Consolidation.csv", csvLines);
        realEngine.Dispose();
    }
    
    [Fact]
    [Trait("Category", "IsolationProfiling")]
    public async Task Profile_4_ClassicalML()
    {
        var emptyPipeline = new LinearPipelineCompositePreprocessor(new IDataPreprocessor[] { new PassThroughStubPreprocessor(FeatureDimension) });
        var classes = new[] { new ClassificationClass("Idle"), new ClassificationClass("Active") };
        var realConsolidator = new SlidingWindowHysteresisResultConsolidator(
            SampleRateHz, new StreamTime(TimeSpan.Zero), TimeSpan.FromSeconds(1), classes, 0.8f, 0.2f);

        var mlContext = new MLContext();
        var dummyData = mlContext.Data.LoadFromEnumerable(new List<TreeDataRow>());
        var mlPipeline = mlContext.Transforms.CustomMapping(
            (TreeDataRow input, TreePrediction output) => { output.Probabilities = new float[classes.Length]; }, 
            contractName: "DummyMapping");
        var predictionEngine = mlContext.Model.CreatePredictionEngine<TreeDataRow, TreePrediction>(mlPipeline.Fit(dummyData));

        var mlNetEngine = new TreeInferenceEngine(
            predictionEngine, seqLength: 10, featureDim: FeatureDimension, stride: 2, numClasses: classes.Length);
            
        var trackingProxy = new ProfilingRawEngineProxy(mlNetEngine);
        
        var realOrchestrator = new InferenceEngine(trackingProxy, realConsolidator, emptyPipeline);
        var repo = new StubResultRepository();

        using var infiniteStream = new InfiniteDummyStream();
        var receiver = new StreamingReceiver(infiniteStream, realOrchestrator, repo);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var csvLines = RunIsolationLogger(trackingProxy, out var timer);
        await receiver.RunAsync(cts.Token);
        timer.Change(Timeout.Infinite, Timeout.Infinite);

        ExportCsv("Profile_4_MLNET.csv", csvLines);
        realOrchestrator.Dispose();
    }
    
    [Fact]
    [Trait("Category", "IsolationProfiling")]
    public async Task Profile_5_DeepLearning()
    {
        var emptyPipeline = new LinearPipelineCompositePreprocessor(new IDataPreprocessor[] { new PassThroughStubPreprocessor(FeatureDimension) });
        var classes = new[] { new ClassificationClass("Idle"), new ClassificationClass("Active") };
        var realConsolidator = new SlidingWindowHysteresisResultConsolidator(
            SampleRateHz, new StreamTime(TimeSpan.Zero), TimeSpan.FromSeconds(1), classes, 0.8f, 0.2f);

        var cnnModel = new TimeSeriesCnn(
            "ProfilingCnn", FeatureDimension, new[] { 16, 32 }, 3, classes.Length);
            
        var torchEngine = new TorchInferenceEngine<MaxMerger>(
            cnnModel, torch.CPU, seqLength: 10, featureDim: FeatureDimension, stride: 2, numClasses: classes.Length);

        var trackingProxy = new ProfilingRawEngineProxy(torchEngine);
        
        var realOrchestrator = new InferenceEngine(trackingProxy, realConsolidator, emptyPipeline);
        var repo = new StubResultRepository();

        using var infiniteStream = new InfiniteDummyStream();
        var receiver = new StreamingReceiver(infiniteStream, realOrchestrator, repo);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var csvLines = RunIsolationLogger(trackingProxy, out var timer);
        await receiver.RunAsync(cts.Token);
        timer.Change(Timeout.Infinite, Timeout.Infinite);

        ExportCsv("Profile_5_Torch.csv", csvLines);
        
        realOrchestrator.Dispose();
        cnnModel.Dispose();
    }

    private void ExportCsv(string fileName, List<string> lines)
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var exportPath = Path.Combine(desktopPath, fileName);
        File.WriteAllLines(exportPath, lines);
    }
    
    private class PureStubRawEngine : IRawInferenceEngine
    {
        public long TotalChunksProcessed;
        private readonly int _classes;

        public PureStubRawEngine(int classes) => _classes = classes;

        public void Classify(PipelineChunk chunk)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            
            chunk.TransitionToInference(_classes);
            Interlocked.Increment(ref TotalChunksProcessed);
        }

        public void Dispose() { }
    }

    private class PureStubConsolidator : IResultConsolidator
    {
        public void Consolidate(PipelineChunk chunk) 
        { 
            chunk.TransitionToConsolidated(Array.Empty<ResultEntry>()); 
        }
    }
    
    private class SyncStubOrchestrator : IInferenceEngine
    {
        private readonly IRawInferenceEngine _rawEngine;
        private readonly Task<ResultEntry[]> _emptyResult = Task.FromResult(Array.Empty<ResultEntry>());

        public SyncStubOrchestrator(IRawInferenceEngine rawEngine) => _rawEngine = rawEngine;
        public int InputDimension => FeatureDimension;

        public Task<ResultEntry[]> ClassifyAsync(PipelineChunk chunk, CancellationToken ct = default)
        {
            _rawEngine.Classify(chunk);
            chunk.TransitionToConsolidated(Array.Empty<ResultEntry>());
            return _emptyResult;
        }
        public void Dispose() { }
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

    private class ProfilingRawEngineProxy : IRawInferenceEngine
    {
        private readonly IRawInferenceEngine _inner;
        public long TotalChunksProcessed;

        public ProfilingRawEngineProxy(IRawInferenceEngine inner) => _inner = inner;

        public void Classify(PipelineChunk chunk)
        {
            Interlocked.Increment(ref TotalChunksProcessed);
            _inner.Classify(chunk);
        }
        public void Dispose() => _inner.Dispose();
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
    
    private List<string> RunIsolationLogger(PureStubRawEngine engine, out Timer timer)
    {
        var csvLines = new List<string> { "Time(Seconds),CurrentHeapSize(MB),CumulativeAllocations(MB),Gen0_Collections,TotalChunks,CpuUsage(%)" };
        var startTime = DateTime.UtcNow;
        long startAllocated = GC.GetTotalAllocatedBytes(true);
        int startGen0 = GC.CollectionCount(0);

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

            var currentHeapMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            var cumulativeAllocMb = (GC.GetTotalAllocatedBytes(true) - startAllocated) / (1024.0 * 1024.0);
            var gen0Collections = GC.CollectionCount(0) - startGen0;
            long currentChunks = Interlocked.Read(ref engine.TotalChunksProcessed);

            lock (csvLines)
            {
                csvLines.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"{Math.Min(60.0, Math.Round(elapsed, 1))},{currentHeapMb:F3},{cumulativeAllocMb:F3},{gen0Collections},{currentChunks},{cpuUsage:F2}"));
            }
        }, null, 0, 100);

        return csvLines;
    }
    
    private List<string> RunIsolationLogger(ProfilingRawEngineProxy engine, out Timer timer)
    {
        var csvLines = new List<string> { "Time(Seconds),CurrentHeapSize(MB),CumulativeAllocations(MB),Gen0_Collections,TotalChunks,CpuUsage(%)" };
        var startTime = DateTime.UtcNow;
        long startAllocated = GC.GetTotalAllocatedBytes(true);
        int startGen0 = GC.CollectionCount(0);

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

            var currentHeapMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            var cumulativeAllocMb = (GC.GetTotalAllocatedBytes(true) - startAllocated) / (1024.0 * 1024.0);
            var gen0Collections = GC.CollectionCount(0) - startGen0;
            long currentChunks = Interlocked.Read(ref engine.TotalChunksProcessed);

            lock (csvLines)
            {
                csvLines.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"{Math.Min(60.0, Math.Round(elapsed, 1))},{currentHeapMb:F3},{cumulativeAllocMb:F3},{gen0Collections},{currentChunks},{cpuUsage:F2}"));
            }
        }, null, 0, 100);

        return csvLines;
    }
}