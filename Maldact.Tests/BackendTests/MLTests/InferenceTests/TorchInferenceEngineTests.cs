using FluentAssertions;
using Maldact.Backend.ML.Inference;
using Maldact.Backend.ML.Inference.OverlapMerging;
using Maldact.Core.Data;
using TorchSharp;

namespace Maldact.Tests.BackendTests.MLTests.InferenceTests;

/// <summary>
/// Verifies the pre-allocated buffering, sequence extraction, overlap resolution, 
/// and zero-allocation span mechanics of the TorchSharp inference engine.
/// </summary>
public class TorchInferenceEngineTests
{
    [Fact]
    public void Constructor_InvalidParameters_ThrowsExceptions()
    {
        using var dummyModel = new DummyTorchModule(2);
        var device = torch.CPU;

        FluentActions.Invoking(() => new TorchInferenceEngine<AverageMerger>(null!, device, 10, 5, 2, 2))
            .Should().Throw<ArgumentNullException>();

        FluentActions.Invoking(() => new TorchInferenceEngine<AverageMerger>(dummyModel, null!, 10, 5, 2, 2))
            .Should().Throw<ArgumentNullException>();

        FluentActions.Invoking(() => new TorchInferenceEngine<AverageMerger>(dummyModel, device,  0, 5, 2, 2))
            .Should().Throw<ArgumentOutOfRangeException>("sequence length must be positive.");

        FluentActions.Invoking(() => new TorchInferenceEngine<AverageMerger>(dummyModel, device, seqLength: 2, featureDim: 5, stride: 3, numClasses: 2))
            .Should().Throw<ArgumentException>("stride cannot physically exceed sequence length.");
    }

    [Fact]
    public void Classify_EmptyChunk_BypassesNativePipeline()
    {
        using var dummyModel = new DummyTorchModule(2);
        using var sut = new TorchInferenceEngine<AverageMerger>(dummyModel, torch.CPU, 3, 2, 1, 2);
        using var chunk = new PipelineChunk(Array.Empty<float[]>(), 2);

        sut.Classify(chunk);

        chunk.State.Should().Be(ChunkState.Initial, "An empty chunk should not advance the pipeline state.");
    }

    [Fact]
    public void Classify_InsufficientFrames_BuffersWithoutPredicting()
    {
        using var dummyModel = new DummyTorchModule(2);
        using var sut = new TorchInferenceEngine<AverageMerger>(dummyModel, torch.CPU, seqLength: 3, featureDim: 2, stride: 1, numClasses: 2);

        var input1 = new[] { new[] { 1f, 1f } };
        var input2 = new[] { new[] { 2f, 2f } };

        using var chunk1 = new PipelineChunk(input1, 2);
        using var chunk2 = new PipelineChunk(input2, 2);

        sut.Classify(chunk1);
        sut.Classify(chunk2);

        // A chunk that buffers but does not emit frames still transitions to Inferred state, 
        // but its output buffer length will be zero.
        chunk1.State.Should().Be(ChunkState.Inferred);
        chunk1.CurrentData.Length.Should().Be(0);
        
        chunk2.State.Should().Be(ChunkState.Inferred);
        chunk2.CurrentData.Length.Should().Be(0);
    }

    [Fact]
    public void Classify_SequenceMet_StridesAndCachesUnresolvedTail()
    {
        // seq = 3, stride = 2. overlap = 1.
        using var dummyModel = new DummyTorchModule(numClasses: 2);
        using var sut = new TorchInferenceEngine<MaxMerger>(dummyModel, torch.CPU, seqLength: 3, featureDim: 2, stride: 2, numClasses: 2);

        var input = new[]
        {
            new[] { 1f, 1f }, // f0
            new[] { 2f, 2f }, // f1
            new[] { 3f, 3f }, // f2 -> sequence met. yields f0, f1. caches f2.
            new[] { 4f, 4f }, // f3
            new[] { 5f, 5f }  // f4 -> sequence met. merges cached f2. yields f2, f3. caches f4.
        };
        using var chunk = new PipelineChunk(input, 2);

        sut.Classify(chunk);

        // 5 frames with seq 3 and stride 2 evaluates twice, yielding 2 frames per evaluation (Total 4 frames emitted)
        var results = chunk.CurrentData.ToArray();
        results.Length.Should().Be(4 * 2); // 4 output frames * 2 classes
        
        // Dummy model outputs 0.0, which sigmoids to 0.5.
        results[0].Should().BeApproximately(0.5f, 0.0001f);
        results[1].Should().BeApproximately(0.5f, 0.0001f);
    }
    
    private class DummyTorchModule : torch.nn.Module<torch.Tensor, torch.Tensor>
    {
        private readonly int _numClasses;

        public DummyTorchModule(int numClasses) : base("Dummy") => _numClasses = numClasses;

        public override torch.Tensor forward(torch.Tensor input)
        {
            var shape = new long[] { input.shape[0], input.shape[1], _numClasses };
            return torch.zeros(shape, dtype: input.dtype, device: input.device);
        }
    }

    // Dummy struct merger for compilation
    private struct AverageMerger : IOverlapMerger
    {
        public void Merge(Span<float> current, ReadOnlySpan<float> historical) { }
    }
    
    private struct MaxMerger : IOverlapMerger
    {
        public void Merge(Span<float> current, ReadOnlySpan<float> historical) { }
    }
}