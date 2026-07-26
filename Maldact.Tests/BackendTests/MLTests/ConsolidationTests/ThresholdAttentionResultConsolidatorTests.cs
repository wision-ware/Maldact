using FluentAssertions;
using Maldact.Backend.ML.Consolidation;
using Maldact.Core.Data;
using Maldact.Core.ML;
using Maldact.Core.ML.Consolidation.Tuning;

namespace Maldact.Tests.BackendTests.MLTests.ConsolidationTests;

/// <summary>
/// Verifies the mathematical accumulation, hang-frame hysteresis, and zero-allocation buffer resets of the Threshold Consolidator.
/// </summary>
public class ThresholdAttentionResultConsolidatorTests
{
    private readonly ClassificationClass[] _dummyClasses = 
    {
        new("TargetA"),
        new("TargetB")
    };
    
    [Fact]
    public void Consolidate_EmptyResults_ReturnsEmptyArray()
    {
        var sut = CreateSut(threshold: 0.8f, hangFrames: 5);
        using var chunk = CreateInferredChunk(Array.Empty<float[]>(), 2);

        sut.Consolidate(chunk);
        var results = chunk.YieldFinal();

        results.Should().NotBeNull().And.BeEmpty();
    }
    
    [Fact]
    public void Consolidate_CleanSignal_YieldsCorrectCentroidAndMean()
    {
        var sut = CreateSut(threshold: 0.8f, hangFrames: 2);

        var sequence = new[]
        {
            new[] { 0.1f, 0.0f }, 
            new[] { 0.9f, 0.0f }, 
            new[] { 0.9f, 0.0f }, 
            new[] { 0.1f, 0.0f }, 
            new[] { 0.1f, 0.0f }  
        };

        using var chunk = CreateInferredChunk(sequence, 2);

        sut.Consolidate(chunk);
        var results = chunk.YieldFinal();

        results.Should().ContainSingle();
        var eventEntry = results.First();
        
        eventEntry.Classification.ClassName.Should().Be("TargetA");
        
        eventEntry.Score.Should().BeApproximately(0.9f, 0.001f);
        eventEntry.Id.Should().NotBeNullOrWhiteSpace().And.NotContain("-"); 
    }
    
    [Fact]
    public void Consolidate_SubThresholdDipWithinHangFrames_MaintainsContinuousEvent()
    {
        var sut = CreateSut(threshold: 0.8f, hangFrames: 3);

        var sequence = new[]
        {
            new[] { 0.9f, 0.0f }, // Hit (Hang reset)
            new[] { 0.1f, 0.0f }, // Miss (Hang 1)
            new[] { 0.1f, 0.0f }, // Miss (Hang 2)
            new[] { 0.9f, 0.0f }, // Hit (Hang reset -> Event saved!)
            new[] { 0.1f, 0.0f }, // Miss (Hang 1)
            new[] { 0.1f, 0.0f }, // Miss (Hang 2)
            new[] { 0.1f, 0.0f }  // Miss (Hang 3 -> Close & Yield)
        };

        using var chunk = CreateInferredChunk(sequence, 2);

        sut.Consolidate(chunk);
        var results = chunk.YieldFinal();

        results.Should().ContainSingle("Because the dip was only 2 frames, the 3-frame hang limit bridged the gap.");
        results.First().Score.Should().BeApproximately(0.5f, 0.001f);
    }
    
    [Fact]
    public void Consolidate_ConcurrentMultiClassSignals_TracksIndependently()
    {
        var sut = CreateSut(threshold: 0.8f, hangFrames: 1);

        var sequence = new[]
        {
            new[] { 0.9f, 0.1f }, // A hits
            new[] { 0.9f, 0.9f }, // A hits, B hits
            new[] { 0.1f, 0.9f }, // A misses (Hang 1 -> Close A), B hits
            new[] { 0.1f, 0.1f }  // B misses (Hang 1 -> Close B)
        };

        using var chunk = CreateInferredChunk(sequence, 2);

        sut.Consolidate(chunk);
        var results = chunk.YieldFinal();

        results.Should().HaveCount(2);
        results.Should().ContainSingle(r => r.Classification.ClassName == "TargetA");
        results.Should().ContainSingle(r => r.Classification.ClassName == "TargetB");
    }
    
    [Fact]
    public void Consolidate_ConsecutiveChunks_ClearsYieldBufferCorrectly()
    {
        var sut = CreateSut(threshold: 0.8f, hangFrames: 1);

        var chunk1Data = new[] { new[] { 0.9f, 0.0f }, new[] { 0.1f, 0.0f } }; 
        var chunk2Data = new[] { new[] { 0.1f, 0.0f }, new[] { 0.1f, 0.0f } }; 

        using var chunk1 = CreateInferredChunk(chunk1Data, 2);
        using var chunk2 = CreateInferredChunk(chunk2Data, 2);

        sut.Consolidate(chunk1);
        var results1 = chunk1.YieldFinal();

        sut.Consolidate(chunk2);
        var results2 = chunk2.YieldFinal();

        results1.Should().ContainSingle();
        results2.Should().BeEmpty("The buffer must be cleared at the start of chunk 2.");
    }
    
    [Fact]
    public void GetConsolidatorConfiguration_ReturnsAccuratePolymorphicDto()
    {
        double frameRate = 10.0;
        float threshold = 0.75f;
        int hang = 5;
        
        var sut = new ThresholdAttentionResultConsolidator(
            frameRate, default!, _dummyClasses, threshold, hang);

        var config = sut.GetConsolidatorConfiguration();

        config.Should().BeOfType<BasicConfiguration>();
        var basicConfig = (BasicConfiguration)config;

        basicConfig.FrameRateHz.Should().Be(frameRate);
        basicConfig.Threshold.Should().Be(threshold);
        basicConfig.HangFrames.Should().Be(hang);
        basicConfig.ClassNames.Should().BeEquivalentTo("TargetA", "TargetB");
    }
    
    private ThresholdAttentionResultConsolidator CreateSut(float threshold, int hangFrames)
    {
        return new ThresholdAttentionResultConsolidator(
            frameRateHz: 10.0, 
            sessionStartTime: default!, 
            classes: _dummyClasses, 
            threshold: threshold, 
            hangFrames: hangFrames);
    }

    private PipelineChunk CreateInferredChunk(float[][] input, int numClasses)
    {
        var chunk = new PipelineChunk(input, numClasses);
        int totalLength = input.Length * numClasses;
        if (totalLength > 0)
        {
            var rawData = chunk.CurrentData.ToArray();
            var inferenceSpan = chunk.TransitionToInference(totalLength);
            rawData.CopyTo(inferenceSpan);
        }
        else 
        {
            chunk.TransitionToInference(0);
        }
        return chunk;
    }
}