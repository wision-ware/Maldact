using System.Reflection;
using FluentAssertions;
using Maldact.Backend.ML.Builders;
using Maldact.Backend.ML.Consolidation.Tuning.Optimizers;
using Maldact.Backend.ML.Consolidation.Tuning.Optimizers.BruteForce;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.ML;
using Maldact.Core.ML.Consolidation.Tuning;

namespace Maldact.Tests.BackendTests.MLTests.BuildersTests;

/// <summary>
/// Verifies the defensive guard clauses and bitwise flag parsing of the training optimizer builder.
/// </summary>
public class ConsolidatorOptimizerBuilderExtensionsTests
{
    private readonly double _defaultFrameRate = 10.0;
    private readonly ClassificationClass[] _validClasses = { new ClassificationClass("TargetA") };
    
    [Fact]
    public void GetOptimizer_InvalidParameters_ThrowsArgumentExceptions()
    {
        // arrange
        var validConfig = new TrainingConfiguration();

        // act & assert
        FluentActions.Invoking(() => ((TrainingConfiguration)null!).GetOptimizer(_defaultFrameRate, _validClasses))
            .Should().Throw<ArgumentNullException>();

        FluentActions.Invoking(() => validConfig.GetOptimizer(_defaultFrameRate, null!))
            .Should().Throw<ArgumentException>()
            .WithMessage("*classification class must be provided*");

        FluentActions.Invoking(() => validConfig.GetOptimizer(_defaultFrameRate, Array.Empty<ClassificationClass>()))
            .Should().Throw<ArgumentException>()
            .WithMessage("*classification class must be provided*");
    }
    
    [Fact]
    public void GetOptimizer_NoneFlagSelected_ThrowsArgumentException()
    {
        // arrange
        var config = new TrainingConfiguration
        {
            ConsolidationAlgorithms = TrainingConfiguration.ConsolidationAlgorithm.None
        };

        // act & assert
        FluentActions.Invoking(() => config.GetOptimizer(_defaultFrameRate, _validClasses))
            .Should().Throw<ArgumentException>()
            .WithMessage("*specify at least one valid consolidation algorithm*");
    }
    
    [Fact]
    public void GetOptimizer_SingleFlag_BuildsCorrectOptimizer()
    {
        // arrange
        var config = new TrainingConfiguration
        {
            ConsolidationAlgorithms = TrainingConfiguration.ConsolidationAlgorithm.BasicAttention
        };

        // act
        var result = config.GetOptimizer(_defaultFrameRate, _validClasses);

        // assert
        result.Should().BeOfType<SequentialCompositeOptimizer>();
        
        var innerOptimizers = GetInnerOptimizers(result);
        innerOptimizers.Should().HaveCount(1, "only BasicAttention was selected.");
        innerOptimizers.First().Should().BeOfType<ThresholdAttentionBruteForceGridOptimizer>();
    }
    
    [Fact]
    public void GetOptimizer_MultipleFlags_BuildsMatchingOptimizers()
    {
        // arrange
        var config = new TrainingConfiguration
        {
            ConsolidationAlgorithms = 
                TrainingConfiguration.ConsolidationAlgorithm.BasicAttention | 
                TrainingConfiguration.ConsolidationAlgorithm.SlidingWindowHysteresis
        };

        // act
        var result = config.GetOptimizer(_defaultFrameRate, _validClasses);

        // assert
        result.Should().BeOfType<SequentialCompositeOptimizer>();
        
        var innerOptimizers = GetInnerOptimizers(result);
        innerOptimizers.Should().HaveCount(2, "two distinct bitwise flags were combined.");
        
        innerOptimizers.Should().ContainSingle(opt => opt is ThresholdAttentionBruteForceGridOptimizer);
        innerOptimizers.Should().ContainSingle(opt => opt is SlidingWindowHysteresisBruteForceGridOptimizer);
    }
    
    [Fact]
    public void GetOptimizer_AllFlag_BuildsExhaustiveSearchSpace()
    {
        // arrange
        var config = new TrainingConfiguration
        {
            ConsolidationAlgorithms = TrainingConfiguration.ConsolidationAlgorithm.All
        };

        // act
        var result = config.GetOptimizer(_defaultFrameRate, _validClasses);

        // assert
        var innerOptimizers = GetInnerOptimizers(result);
        innerOptimizers.Should().HaveCount(3, "the All flag must map to all three available algorithms.");
    }
    
    private static IConsolidatorOptimizer[] GetInnerOptimizers(IConsolidatorOptimizer composite)
    {
        // Note: Assumes the internal field or property holding the optimizers in SequentialCompositeOptimizer 
        // can be extracted. We look for any underlying array or enumerable of IConsolidatorOptimizer.
        var fields = composite.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
        
        foreach (var field in fields)
        {
            if (field.GetValue(composite) is IEnumerable<IConsolidatorOptimizer> collection)
            {
                return collection.ToArray();
            }
        }

        throw new InvalidOperationException(
            "Could not locate the internal optimizer collection via reflection. " +
            "Ensure SequentialCompositeOptimizer stores its children in a standard private field array or list.");
    }
}