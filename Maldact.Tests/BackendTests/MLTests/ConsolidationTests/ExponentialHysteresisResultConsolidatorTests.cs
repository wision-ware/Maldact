using FluentAssertions;
using Maldact.Backend.ML.Consolidation;
using Maldact.Core.Data;
using Maldact.Core.ML;
using Maldact.Core.ML.Consolidation.Tuning;
using Maldact.Core.Results;

namespace Maldact.Tests.BackendTests.MLTests.ConsolidationTests;

/// <summary>
/// Verifies the exponential decay math, delay buffer alignment, and configuration routing of the EMA consolidator.
/// </summary>
public class ExponentialHysteresisResultConsolidatorTests
{
    private readonly ClassificationClass[] _dummyClasses = 
    {
        new("TargetA"),
        new("TargetB")
    };
    
    [Fact]
    public void Consolidate_EmaCrossesThresholds_YieldsCorrectDelayedEvent()
    {
        var sut = CreateSut(0.5f, 0.6f, 0.1f);

        var sequence = new[]
        {
            new[] { 1.0f, 0.0f }, // f0: ema=0.5
            new[] { 1.0f, 0.0f }, // f1: ema=0.75 -> ACTIVATES (trailing index clamped to 0)
            new[] { 1.0f, 0.0f }, // f2: ema=0.875
            new[] { 0.0f, 0.0f }, // f3: ema=0.4375 
            new[] { 0.0f, 0.0f }, // f4: ema=0.2187
            new[] { 0.0f, 0.0f }, // f5: ema=0.1093
            new[] { 0.0f, 0.0f }  // f6: ema=0.0546 -> DEACTIVATES (trailing index 7-3=4)
        };

        using var chunk = CreateInferredChunk(sequence, 2);

        sut.Consolidate(chunk);
        var results = chunk.YieldFinal();

        results.Should().ContainSingle();
        var eventEntry = results.First();
        
        eventEntry.Classification.ClassName.Should().Be("TargetA");
        
        eventEntry.StartTime.TotalMilliseconds.Should().Be(0.0);
        eventEntry.EndTime.TotalMilliseconds.Should().Be(400.0);
        
        eventEntry.Score.Should().BeGreaterThan(0.0f);
    }
    
    [Fact]
    public void Consolidate_ConsecutiveChunks_ClearsYieldBufferCorrectly()
    {
        var sut = CreateSut(0.5f, 0.6f, 0.1f);

        var chunk1Data = new[]
        {
            new[] { 1.0f, 0.0f }, 
            new[] { 1.0f, 0.0f }, // ACTIVATES
            new[] { 0.0f, 0.0f }, 
            new[] { 0.0f, 0.0f }, 
            new[] { 0.0f, 0.0f }, 
            new[] { 0.0f, 0.0f }  // DEACTIVATES -> yields 1
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
        results2.Should().BeEmpty("the yield buffer must be reset at the start of the second chunk.");
    }
    
    [Fact]
    public void GetConsolidatorConfiguration_ReturnsAccuratePolymorphicDto()
    {
        var sut = new ExponentialHysteresisResultConsolidator(
            frameRateHz: 10.0, 
            sessionStartTime: new StreamTime("0"), 
            classes: _dummyClasses, 
            alpha: 0.5f, 
            activationThreshold: 0.8f, 
            deactivationThreshold: 0.4f);

        var config = sut.GetConsolidatorConfiguration();

        config.Should().BeOfType<ExponentialConfiguration>();
        var dto = (ExponentialConfiguration)config;

        dto.Alpha.Should().Be(0.5f);
        dto.ActivationDensity.Should().Be(0.8f);
        dto.DeactivationDensity.Should().Be(0.4f);
        dto.FrameRateHz.Should().Be(10.0);
        dto.ClassNames.Should().BeEquivalentTo("TargetA", "TargetB");
    }

    private ExponentialHysteresisResultConsolidator CreateSut(
        float alpha, float activationThreshold, float deactivationThreshold)
    {
        return new ExponentialHysteresisResultConsolidator(
            frameRateHz: 10.0, 
            sessionStartTime: new StreamTime("0"), 
            classes: _dummyClasses, 
            alpha: alpha, 
            activationThreshold: activationThreshold, 
            deactivationThreshold: deactivationThreshold);
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