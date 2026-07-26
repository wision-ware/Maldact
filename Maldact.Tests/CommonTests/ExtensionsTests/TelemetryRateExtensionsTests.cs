using FluentAssertions;
using Maldact.Common.Configuration.Extensions;
using Maldact.Core.Config.ConfigDefinitions;

namespace Maldact.Tests.CommonTests.ExtensionsTests;

/// <summary>
/// Verifies the correctness of telemetry rate calculations across pipeline transformations.
/// </summary>
public class TelemetryRateExtensionTests
{
    [Fact]
    public void GetFrameRate_TreeEnsemble_CalculatesCorrectly()
    {
        // arrange
        var model = new ModelSpecification
        {
            Algorithm = ModelSpecification.AlgorithmType.TreeEnsemble,
            WindowStride = 4
        };
        double inputSampleRate = 100.0;

        // act
        var result = model.GetFrameRate(inputSampleRate);

        // assert
        result.Should().Be(25.0);
    }

    [Fact]
    public void GetFrameRate_StandardAlgorithm_ReturnsUnmodifiedSampleRate()
    {
        // arrange
        var model = new ModelSpecification
        {
            Algorithm = ModelSpecification.AlgorithmType.Cnn
        };
        double inputSampleRate = 100.0;

        // act
        var result = model.GetFrameRate(inputSampleRate);

        // assert
        result.Should().Be(100.0);
    }

    [Fact]
    public void GetFrameRate_MissingAlgorithm_ThrowsException()
    {
        // arrange
        var model = new ModelSpecification { Algorithm = null };

        // act & assert
        FluentActions.Invoking(() => model.GetFrameRate(100.0))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetSampleRate_PipelineWithResampling_ReturnsTerminalRate()
    {
        // arrange
        var contract = new PreprocessingContract
        {
            InputSampleRate = 100.0,
            Pipeline = new List<PreprocessingStep>
            {
                new() { Type = PreprocessingStep.PreprocessingStepType.Normalization, Normalization = new() { Method = NormalizationOptions.NormalizationMethod.ZScore } },
                new() { Type = PreprocessingStep.PreprocessingStepType.Resampling, Resampling = new() { TargetHz = 50.0 } },
                new() { Type = PreprocessingStep.PreprocessingStepType.Resampling, Resampling = new() { TargetHz = 25.0 } } // terminal step
            }
        };

        // act
        var result = contract.GetSampleRate();

        // assert
        result.Should().Be(25.0);
    }

    [Fact]
    public void GetSampleRate_NoResampling_ReturnsInputRate()
    {
        // arrange
        var contract = new PreprocessingContract
        {
            InputSampleRate = 100.0,
            Pipeline = new List<PreprocessingStep>
            {
                new() { Type = PreprocessingStep.PreprocessingStepType.Imputation, Imputation = new() { Method = ImputationOptions.ImputationMethod.ZeroFill } }
            }
        };

        // act
        var result = contract.GetSampleRate();

        // assert
        result.Should().Be(100.0);
    }

    [Fact]
    public void GetSampleRate_MissingInputRate_ThrowsException()
    {
        // arrange
        var contract = new PreprocessingContract { InputSampleRate = null };

        // act & assert
        FluentActions.Invoking(() => contract.GetSampleRate())
            .Should().Throw<InvalidOperationException>();
    }
}