using FluentAssertions;
using Maldact.Backend.ML.Consolidation;
using Maldact.Backend.ML.Consolidation.Tuning.Optimizers.BruteForce;
using Maldact.Core.ML;
using Maldact.Core.ML.Consolidation.Tuning;

namespace Maldact.Tests.BackendTests.MLTests.ConsolidationTests.TuningTests.OptimizersTests.BruteForceTests;

/// <summary>
/// Verifies the mathematical permutation generation, formula derivations, and exhaustion mechanics 
/// of the brute-force hyperparameter grid search optimizers.
/// </summary>
public class BruteForceGridOptimizerTests
{
    private const double StandardFrameRateHz = 10.0;
    
    private readonly ClassificationClass[] _dummyClasses = 
    {
        new("TargetA")
    };
    
    [Fact]
    public void ExponentialHysteresisBruteForce_GeneratesCorrectGridAndAlpha()
    {
        // arrange
        using var sut = new ExponentialHysteresisBruteForceGridOptimizer(StandardFrameRateHz, _dummyClasses);
        var yieldedCandidates = new List<ITunableResultConsolidator>();

        // act
        ITunableResultConsolidator? current;
        while ((current = sut.SuggestNext(null)) != null)
        {
            yieldedCandidates.Add(current);
        }

        // assert
        
        // verify type instantiation
        var firstCandidate = yieldedCandidates[0];
        firstCandidate.Should().BeOfType<ExponentialHysteresisResultConsolidator>();

        // verify alpha formula translation for the first frame window (2)
        // alpha = 2 / (2 + 1) = 2/3 = 0.6666...
        var config = (ExponentialConfiguration)firstCandidate.GetConsolidatorConfiguration();
        config.Alpha.Should().BeApproximately(0.6666f, 0.001f, "the window-to-alpha mapping must align with physical EMA formulas.");
    }
    
    [Fact]
    public void SlidingWindowHysteresisBruteForce_GeneratesCorrectGridAndDurations()
    {
        // arrange
        using var sut = new SlidingWindowHysteresisBruteForceGridOptimizer(StandardFrameRateHz, _dummyClasses);
        var yieldedCandidates = new List<ITunableResultConsolidator>();

        // act
        ITunableResultConsolidator? current;
        while ((current = sut.SuggestNext(null)) != null)
        {
            yieldedCandidates.Add(current);
        }

        // assert
        
        var firstCandidate = yieldedCandidates[0];
        firstCandidate.Should().BeOfType<SlidingWindowHysteresisResultConsolidator>();

        // verify frame-to-timespan mapping at 10Hz for the first frame window (2)
        // duration = 2 frames / 10Hz = 0.2 seconds
        var config = (SlidingWindowConfiguration)firstCandidate.GetConsolidatorConfiguration();
        config.WindowDuration.TotalSeconds.Should().BeApproximately(0.2, 0.001, "frame limits must be accurately translated into absolute chronological spans.");
    }
    
    [Fact]
    public void ThresholdAttentionBruteForce_GeneratesCorrectGrid()
    {
        // arrange
        using var sut = new ThresholdAttentionBruteForceGridOptimizer(StandardFrameRateHz, _dummyClasses);
        var yieldedCandidates = new List<ITunableResultConsolidator>();

        // act
        ITunableResultConsolidator? current;
        while ((current = sut.SuggestNext(null)) != null)
        {
            yieldedCandidates.Add(current);
        }

        // assert
        yieldedCandidates[0].Should().BeOfType<ThresholdAttentionResultConsolidator>();
    }
    
    [Fact]
    public void SuggestNext_ExhaustedGrid_ReturnsNullIndefinitely()
    {
        // arrange
        using var sut = new ThresholdAttentionBruteForceGridOptimizer(StandardFrameRateHz, _dummyClasses);
        
        // exhaust the 25 permutations
        for (int i = 0; i < 25; i++)
        {
            sut.SuggestNext(null);
        }

        // act
        var finalCall = sut.SuggestNext(null);
        var overrunCall = sut.SuggestNext(null);

        // assert
        finalCall.Should().BeNull("the grid enumerator has reached the end of its permutations.");
        overrunCall.Should().BeNull("repeated calls on an exhausted optimizer must safely yield null.");
    }
    
    [Fact]
    public void Dispose_SafelyTearsDownEnumeratorWithoutThrowing()
    {
        // arrange
        var sut = new ThresholdAttentionBruteForceGridOptimizer(StandardFrameRateHz, _dummyClasses);
        
        // advance the enumerator to lock state
        sut.SuggestNext(null);

        // act
        Action act = () => sut.Dispose();

        // assert
        act.Should().NotThrow("IDisposable implementation must silently flush the iterator state machine.");
    }
}