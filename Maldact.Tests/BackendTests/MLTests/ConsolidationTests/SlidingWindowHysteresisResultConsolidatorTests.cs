using FluentAssertions;
using Maldact.Backend.ML.Consolidation;
using Maldact.Core.Data;
using Maldact.Core.ML;
using Maldact.Core.ML.Consolidation.Tuning;
using Maldact.Core.Results;

namespace Maldact.Tests.BackendTests.MLTests.ConsolidationTests;

/// <summary>
/// Verifies the delayed-reader mechanics, boundary clamping, and O(1) centroid math of the sliding window consolidator.
/// </summary>
public class SlidingWindowHysteresisResultConsolidatorTests
{
    private readonly ClassificationClass[] _dummyClasses = 
    {
        new("TargetA"),
        new("TargetB")
    };
    
    [Fact]
    public void Consolidate_EmptyResults_ReturnsEmptyArray()
    {
        var sut = CreateSut(TimeSpan.FromSeconds(1), 0.8f, 0.4f);
        using var chunk = CreateInferredChunk(Array.Empty<float[]>(), 2);

        sut.Consolidate(chunk);
        var results = chunk.YieldFinal();

        results.Should().NotBeNull().And.BeEmpty();
    }
    
    [Fact]
    public void Consolidate_DelayedReader_ExtractsAccurateBoundingBoxAndCentroid()
    {
        var sut = CreateSut(TimeSpan.FromSeconds(0.3), 0.8f, 0.1f);

        var sequence = new[]
        {
            new[] { 1.0f, 0.0f }, // f0: accum=1
            new[] { 1.0f, 0.0f }, // f1: accum=2
            new[] { 1.0f, 0.0f }, // f2: accum=3 -> ACTIVATES (trailing index 0)
            new[] { 1.0f, 0.0f }, // f3: accum=3 (exiting f0=1). centroid mass += 1*1. active.
            new[] { 0.0f, 0.0f }, // f4: accum=2 (exiting f1=1). centroid mass += 2*1. active.
            new[] { 0.0f, 0.0f }, // f5: accum=1 (exiting f2=1). centroid mass += 3*1. active.
            new[] { 0.0f, 0.0f }  // f6: accum=0 (exiting f3=1). centroid mass += 4*1. -> DEACTIVATES (trailing index 4)
        };

        using var chunk = CreateInferredChunk(sequence, 2);

        sut.Consolidate(chunk);
        var results = chunk.YieldFinal();

        results.Should().ContainSingle();
        var eventEntry = results.First();
        
        eventEntry.Classification.ClassName.Should().Be("TargetA");
        
        eventEntry.StartTime.TotalMilliseconds.Should().Be(0.0);
        eventEntry.EndTime.TotalMilliseconds.Should().Be(400.0);
        
        eventEntry.Score.Should().Be(1.0f);
        eventEntry.CentroidTime.TotalMilliseconds.Should().Be(250.0);
    }
    
    [Fact]
    public void Consolidate_EarlyActivation_SafelyClampsBoundaries()
    {
        var sut = CreateSut(TimeSpan.FromSeconds(0.5), 0.1f, 0.05f);

        var sequence = new[]
        {
            new[] { 1.0f, 0.0f }, // f0: accum=1. ACTIVATES IMMEDIATELY! 
            new[] { 0.0f, 0.0f }, // f1
            new[] { 0.0f, 0.0f }, // f2
            new[] { 0.0f, 0.0f }, // f3
            new[] { 0.0f, 0.0f }, // f4
            new[] { 0.0f, 0.0f }  // f5: exiting f0=1 -> DEACTIVATES.
        };

        using var chunk = CreateInferredChunk(sequence, 2);

        sut.Consolidate(chunk);
        var results = chunk.YieldFinal();

        results.Should().ContainSingle();
        var eventEntry = results.First();
        
        eventEntry.StartTime.TotalMilliseconds.Should().Be(0.0, "The trailing boundary must be clamped to 0 on early activation.");
        float.IsNaN(eventEntry.Score).Should().BeFalse();
    }
    
    [Fact]
    public void Consolidate_ConsecutiveChunks_ClearsYieldBufferCorrectly()
    {
        var sut = CreateSut(TimeSpan.FromSeconds(0.2), 0.5f, 0.1f);

        var chunk1Data = new[]
        {
            new[] { 1.0f, 0.0f }, 
            new[] { 1.0f, 0.0f }, // ACTIVATES
            new[] { 0.0f, 0.0f }, 
            new[] { 0.0f, 0.0f }, // DEACTIVATES -> yields 1
            new[] { 0.0f, 0.0f }
        };
        
        var chunk2Data = new[]
        {
            new[] { 0.0f, 0.0f }, 
            new[] { 0.0f, 0.0f }  // yields 0
        };

        using var chunk1 = CreateInferredChunk(chunk1Data, 2);
        using var chunk2 = CreateInferredChunk(chunk2Data, 2);

        sut.Consolidate(chunk1);
        var results1 = chunk1.YieldFinal();

        sut.Consolidate(chunk2);
        var results2 = chunk2.YieldFinal();

        results1.Should().ContainSingle();
        results2.Should().BeEmpty("The yield buffer must be reset at the start of the second chunk.");
    }
    
    [Fact]
    public void GetConsolidatorConfiguration_ReturnsAccuratePolymorphicDto()
    {
        var window = TimeSpan.FromSeconds(2);
        var sut = new SlidingWindowHysteresisResultConsolidator(
            sampleRateHz: 10.0, 
            sessionStartTime: new StreamTime("0"), 
            windowDuration: window, 
            classes: _dummyClasses, 
            activationDensity: 0.8f, 
            deactivationDensity: 0.4f);

        var config = sut.GetConsolidatorConfiguration();

        config.Should().BeOfType<SlidingWindowConfiguration>();
        var dto = (SlidingWindowConfiguration)config;

        dto.WindowDuration.Should().Be(window);
        dto.ActivationDensity.Should().Be(0.8f);
        dto.DeactivationDensity.Should().Be(0.4f);
        dto.FrameRateHz.Should().Be(10.0);
        dto.ClassNames.Should().BeEquivalentTo("TargetA", "TargetB");
    }
    
    private SlidingWindowHysteresisResultConsolidator CreateSut(
        TimeSpan window, float activationDensity, float deactivationDensity)
    {
        return new SlidingWindowHysteresisResultConsolidator(
            sampleRateHz: 10.0, 
            sessionStartTime: new StreamTime("0"), 
            windowDuration: window, 
            classes: _dummyClasses, 
            activationDensity: activationDensity, 
            deactivationDensity: deactivationDensity);
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