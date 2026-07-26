using FluentAssertions;
using Maldact.Backend.ML.Inference;
using Maldact.Backend.ML.Modules;
using Maldact.Core.Data;
using Microsoft.ML;

namespace Maldact.Tests.BackendTests.MLTests.InferenceTests;

/// <summary>
/// Verifies the zero-allocation buffering, chronological flattening math, and stride mechanics 
/// of the classical ML.NET tree inference engine operating on PipelineChunks.
/// </summary>
public class TreeInferenceEngineTests
{
    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentExceptions()
    {
        var validEngine = CreateSpyEngine(_ => { });

        FluentActions.Invoking(() => new TreeInferenceEngine(null!, 10, 5, 2, 2))
            .Should().Throw<ArgumentNullException>();

        FluentActions.Invoking(() => new TreeInferenceEngine(validEngine, 0, 5, 2, 2))
            .Should().Throw<ArgumentOutOfRangeException>("sequence length must be positive.");

        FluentActions.Invoking(() => new TreeInferenceEngine(validEngine, 10, 0, 2, 2))
            .Should().Throw<ArgumentOutOfRangeException>("feature dimension must be positive.");

        FluentActions.Invoking(() => new TreeInferenceEngine(validEngine, 10, 5, 0, 2))
            .Should().Throw<ArgumentOutOfRangeException>("stride must be positive.");
    }

    [Fact]
    public void Classify_EmptyOrNullChunk_BypassesNativePipeline()
    {
        using var sut = new TreeInferenceEngine(CreateSpyEngine(_ => { }), seqLength: 3, featureDim: 2, stride: 1, numClasses: 2);
        using var chunk = new PipelineChunk(Array.Empty<float[]>(), 2);

        sut.Classify(chunk);

        chunk.State.Should().Be(ChunkState.Initial);
    }

    [Fact]
    public void Classify_InsufficientFrames_BuffersWithoutPredicting()
    {
        // requires 3 frames
        using var sut = new TreeInferenceEngine(CreateSpyEngine(_ => { }), seqLength: 3, featureDim: 2, stride: 1, numClasses: 2);

        var input1 = new[] { new[] { 1f, 1f } };
        var input2 = new[] { new[] { 2f, 2f } };

        using var chunk1 = new PipelineChunk(input1, 2);
        using var chunk2 = new PipelineChunk(input2, 2);

        sut.Classify(chunk1);
        sut.Classify(chunk2);

        chunk1.State.Should().Be(ChunkState.Inferred);
        chunk1.CurrentData.Length.Should().Be(0);
        
        chunk2.State.Should().Be(ChunkState.Inferred);
        chunk2.CurrentData.Length.Should().Be(0);
    }

    [Fact]
    public void Classify_StreamExceedsSequence_StridesAndYieldsMultiplePredictions()
    {
        // seq = 3, stride = 2. 
        using var sut = new TreeInferenceEngine(CreateSpyEngine(_ => { }), seqLength: 3, featureDim: 2, stride: 2, numClasses: 2);

        var input = new[]
        {
            new[] { 1f, 1f }, // f0
            new[] { 2f, 2f }, // f1
            new[] { 3f, 3f }, // f2 -> seq met. yields 1. stride drops f0, f1. buffer: f2.
            new[] { 4f, 4f }, // f3 -> buffer: f2, f3.
            new[] { 5f, 5f }  // f4 -> seq met. yields 2. stride drops f2, f3. buffer: f4.
        };
        using var chunk = new PipelineChunk(input, 2);

        sut.Classify(chunk);

        var results = chunk.CurrentData.ToArray();
        // 5 frames with seq 3 and stride 2 yields exactly 2 inferences (4 floats output total for 2 classes)
        results.Length.Should().Be(4); 
        
        results[0].Should().Be(0.8f);
        results[1].Should().Be(0.2f);
    }

    [Fact]
    public void Classify_FlattensTargetedSequenceCorrectlyIntoSharedBuffer()
    {
        var interceptedFeatures = new List<float[]>();
        var spyEngine = CreateSpyEngine(features => interceptedFeatures.Add(features.ToArray()));
        
        using var sut = new TreeInferenceEngine(spyEngine, seqLength: 2, featureDim: 2, stride: 1, numClasses: 2);

        var input = new[]
        {
            new[] { 1f, 2f }, // f0
            new[] { 3f, 4f }, // f1 -> predicts [1, 2, 3, 4]
            new[] { 5f, 6f }  // f2 -> predicts [3, 4, 5, 6] due to stride 1
        };
        using var chunk = new PipelineChunk(input, 2);

        sut.Classify(chunk);

        interceptedFeatures.Should().HaveCount(2);

        interceptedFeatures[0].Should().HaveCount(4, "2 frames * 2 features = flat array of 4.");
        interceptedFeatures[0].Should().ContainInOrder(1f, 2f, 3f, 4f);

        interceptedFeatures[1].Should().ContainInOrder(3f, 4f, 5f, 6f);
    }

    // --- ML.NET SPIES ---

    private static PredictionEngine<TreeDataRow, TreePrediction> CreateSpyEngine(Action<float[]> onPredict)
    {
        var mlContext = new MLContext();
        var dummyData = mlContext.Data.LoadFromEnumerable(new[] 
        { 
            new TreeDataRow { Features = Array.Empty<float>(), Label = 0 } 
        });

        Action<TreeDataRow, TreePrediction> mapAction = (input, output) =>
        {
            onPredict(input.Features);
            output.Probabilities = new[] { 0.8f, 0.2f }; 
        };

        var pipeline = mlContext.Transforms.CustomMapping(mapAction, contractName: null);
        var model = pipeline.Fit(dummyData);
        
        return mlContext.Model.CreatePredictionEngine<TreeDataRow, TreePrediction>(model);
    }
}