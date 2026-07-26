using FluentAssertions;
using Maldact.Backend.ML.Consolidation;
using Maldact.Core.ML;
using Maldact.Core.Results;

namespace Maldact.Tests.BackendTests.MLTests.ConsolidationTests;

/// <summary>
/// Verifies the delayed-reader mechanics, flat ring buffer bounds, and memory reuse of the generic consolidation engine.
/// </summary>
public class SlidingWindowConsolidationEngineTests
{
    private readonly ClassificationClass[] _dummyClasses = 
    {
        new("TargetA")
    };
    
    [Fact]
    public void Consolidate_EmptyResults_ReturnsEmptyArray()
    {
        var sut = CreateSut(3, 0.8f, 0.4f);

        var results = sut.Consolidate(ReadOnlySpan<float>.Empty);

        results.Should().NotBeNull().And.BeEmpty();
    }
    
    [Fact]
    public void Consolidate_EarlyActivation_SafelyClampsBoundariesToZero()
    {
        var sut = CreateSut(5, 0.1f, 0.05f);

        var sequence = new[]
        {
            new[] { 1.0f }, // f0: accum=1. activates immediately before ring buffer is full
            new[] { 0.0f }, // f1
            new[] { 0.0f }, // f2
            new[] { 0.0f }, // f3
            new[] { 0.0f }, // f4
            new[] { 0.0f }  // f5: exiting f0=1 -> deactivates.
        };

        var results = sut.Consolidate(Flatten(sequence));

        results.Should().ContainSingle();
        var eventEntry = results.First();
        
        eventEntry.StartTime.TotalMilliseconds.Should().Be(0.0, "the trailing boundary must clamp to 0 on early activation.");
        float.IsNaN(eventEntry.Score).Should().BeFalse("fallback duration clamping should prevent nan division.");
    }

    private SlidingWindowConsolidationEngine<SlidingWindowScorer> CreateSut(
        int windowSize, float actThreshold, float deactThreshold)
    {
        return new SlidingWindowConsolidationEngine<SlidingWindowScorer>(
            sampleRateHz: 10.0, 
            sessionStartTime: new StreamTime("0"), 
            windowSizeSamples: windowSize, 
            classes: _dummyClasses, 
            activationThreshold: actThreshold, 
            deactivationThreshold: deactThreshold, 
            scorerTemplate: new SlidingWindowScorer());
    }

    private float[] Flatten(float[][] input)
    {
        if (input.Length == 0) return Array.Empty<float>();
        int dim = input[0].Length;
        float[] flat = new float[input.Length * dim];
        for(int i = 0; i < input.Length; i++) 
        {
            Array.Copy(input[i], 0, flat, i * dim, dim);
        }
        return flat;
    }
}