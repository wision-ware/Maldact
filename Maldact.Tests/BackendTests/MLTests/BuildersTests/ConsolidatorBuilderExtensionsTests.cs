using FluentAssertions;
using Maldact.Backend.ML.Builders;
using Maldact.Backend.ML.Consolidation;
using Maldact.Core.ML.Consolidation;
using Maldact.Core.ML.Consolidation.Tuning;
using Maldact.Core.Results;

namespace Maldact.Tests.BackendTests.MLTests.BuildersTests;

/// <summary>
/// Verifies the defensive guard clauses and polymorphic routing of the consolidator builder.
/// </summary>
public class ConsolidatorBuilderExtensionsTests
{
    private readonly StreamTime _defaultSessionTime = new(TimeSpan.Zero);
    private readonly string[] _validClassNames = { "TargetA", "TargetB" };

    /// <summary>
    /// Ensures the builder violently rejects a completely null configuration object.
    /// </summary>
    [Fact]
    public void BuildConsolidator_NullConfiguration_ThrowsArgumentNullException()
    {
        // arrange
        ConsolidatorConfiguration? nullConfig = null;

        // act & assert
        FluentActions.Invoking(() => nullConfig!.BuildConsolidator(_defaultSessionTime))
            .Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that configurations missing target classification mappings are caught early,
    /// preventing downstream array access violations.
    /// </summary>
    [Fact]
    public void BuildConsolidator_NullOrEmptyClassNames_ThrowsArgumentException()
    {
        // arrange
        var configWithNullClasses = new BasicConfiguration
        {
            ClassNames = null!,
            Threshold = 0,
            HangFrames = 0,
            FrameRateHz = 0
        };
        var configWithEmptyClasses = new BasicConfiguration
        {
            ClassNames = Array.Empty<string>(),
            Threshold = 0,
            HangFrames = 0,
            FrameRateHz = 0
        };

        // act & assert
        FluentActions.Invoking(() => configWithNullClasses.BuildConsolidator(_defaultSessionTime))
            .Should().Throw<ArgumentException>()
            .WithMessage("*must define at least one target*");

        FluentActions.Invoking(() => configWithEmptyClasses.BuildConsolidator(_defaultSessionTime))
            .Should().Throw<ArgumentException>()
            .WithMessage("*must define at least one target*");
    }

    /// <summary>
    /// Ensures BasicConfiguration strictly maps to the ThresholdAttentionResultConsolidator.
    /// </summary>
    [Fact]
    public void BuildConsolidator_BasicConfiguration_ReturnsThresholdAttentionConsolidator()
    {
        // arrange
        var config = new BasicConfiguration
        {
            ClassNames = _validClassNames,
            FrameRateHz = 10.0,
            Threshold = 0.5f,
            HangFrames = 5
        };

        // act
        var result = config.BuildConsolidator(_defaultSessionTime);

        // assert
        result.Should().BeOfType<ThresholdAttentionResultConsolidator>(
            "the BasicConfiguration DTO directly correlates to the Threshold Attention strategy.");
    }

    /// <summary>
    /// Ensures SlidingWindowConfiguration strictly maps to the SlidingWindowHysteresisResultConsolidator.
    /// </summary>
    [Fact]
    public void BuildConsolidator_SlidingWindowConfiguration_ReturnsSlidingWindowConsolidator()
    {
        // arrange
        var config = new SlidingWindowConfiguration
        {
            ClassNames = _validClassNames,
            FrameRateHz = 10.0,
            WindowDuration = TimeSpan.FromSeconds(2),
            ActivationDensity = 0.8f,
            DeactivationDensity = 0.2f
        };

        // act
        var result = config.BuildConsolidator(_defaultSessionTime);

        // assert
        result.Should().BeOfType<SlidingWindowHysteresisResultConsolidator>();
    }

    /// <summary>
    /// Ensures ExponentialConfiguration strictly maps to the ExponentialHysteresisResultConsolidator.
    /// </summary>
    [Fact]
    public void BuildConsolidator_ExponentialConfiguration_ReturnsExponentialHysteresisConsolidator()
    {
        // arrange
        var config = new ExponentialConfiguration
        {
            ClassNames = _validClassNames,
            FrameRateHz = 10.0,
            Alpha = 0.5f,
            ActivationDensity = 0.8f,
            DeactivationDensity = 0.2f
        };

        // act
        var result = config.BuildConsolidator(_defaultSessionTime);

        // assert
        result.Should().BeOfType<ExponentialHysteresisResultConsolidator>();
    }

    /// <summary>
    /// Verifies that unmapped or future configuration types fail cleanly with a NotSupportedException
    /// rather than throwing blind casting errors or returning null.
    /// </summary>
    [Fact]
    public void BuildConsolidator_UnknownConfiguration_ThrowsNotSupportedException()
    {
        // arrange
        var config = new DummyConfiguration { ClassNames = _validClassNames, FrameRateHz = 10};

        // act & assert
        FluentActions.Invoking(() => config.BuildConsolidator(_defaultSessionTime))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Unknown consolidator strategy: DummyConfiguration*");
    }

    // --- TEST STUBS ---

    /// <summary>
    /// A localized stub used strictly to trigger the default switch arm in the builder.
    /// </summary>
    private record DummyConfiguration : ConsolidatorConfiguration;
}