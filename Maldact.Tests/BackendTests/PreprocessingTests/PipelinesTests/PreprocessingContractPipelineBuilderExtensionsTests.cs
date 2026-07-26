using FluentAssertions;
using Maldact.Backend.Preprocessing.DSPs;
using Maldact.Backend.Preprocessing.Pipelines;
using Maldact.Core.Config.ConfigDefinitions;

namespace Maldact.Tests.BackendTests.PreprocessingTests.PipelinesTests;

public class PreprocessingContractPipelineBuilderExtensionsTests
{
    [Fact]
    public void BuildPipeline_NullContract_ThrowsArgumentNullException()
    {
        // arrange
        PreprocessingContract? contract = null;

        // act
        Action act = () => contract!.BuildPipeline();

        // assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuildPipeline_MissingInputSampleRate_ThrowsArgumentException()
    {
        // arrange
        var contract = new PreprocessingContract
        {
            InputDimension = 10,
            InputSampleRate = null,
            FinalReshaping = new ReshapingOptions { TargetDimension = 10, Method = ReshapingOptions.ReshapeMethod.Strict }
        };

        // act
        Action act = () => contract.BuildPipeline();

        // assert
        act.Should().Throw<ArgumentException>().WithMessage("*InputSampleRate missing*");
    }

    [Fact]
    public void BuildPipeline_MissingInputDimension_ThrowsArgumentException()
    {
        // arrange
        var contract = new PreprocessingContract
        {
            InputDimension = null,
            InputSampleRate = 100.0,
            FinalReshaping = new ReshapingOptions { TargetDimension = 10, Method = ReshapingOptions.ReshapeMethod.Strict }
        };

        // act
        Action act = () => contract.BuildPipeline();

        // assert
        act.Should().Throw<ArgumentException>().WithMessage("*InputDimension missing*");
    }

    [Fact]
    public void BuildPipeline_ValidContract_ConstructsCompletePipeline()
    {
        // arrange
        // we will build a pipeline that:
        // 1. takes 2 features at 100Hz
        // 2. resamples to 50Hz (dimension stays 2)
        // 3. reshapes to 4 features via interpolation
        // 4. final reshapes strictly to 4 features
        var contract = new PreprocessingContract
        {
            InputDimension = 2,
            InputSampleRate = 100.0,
            Pipeline = new List<PreprocessingStep>
            {
                new()
                {
                    Type = PreprocessingStep.PreprocessingStepType.Resampling,
                    Resampling = new ResamplingOptions { TargetHz = 50.0, AggregationFunctionUsed = ResamplingOptions.AggregationFunction.Mean }
                },
                new()
                {
                    Type = PreprocessingStep.PreprocessingStepType.Reshaping,
                    Reshaping = new ReshapingOptions { TargetDimension = 4, Method = ReshapingOptions.ReshapeMethod.Interpolation }
                }
            },
            FinalReshaping = new ReshapingOptions
            {
                TargetDimension = 4,
                Method = ReshapingOptions.ReshapeMethod.Strict
            }
        };

        // act
        var result = contract.BuildPipeline();

        // assert
        result.Should().BeOfType<LinearPipelineCompositePreprocessor>();
        result.InputDimension.Should().Be(2); // must match initial contract
        result.OutputDimension.Should().Be(4); // must match final reshaping
    }

    [Fact]
    public void CreateFilter_ImputationWithoutOptions_ThrowsArgumentNullException()
    {
        // arrange
        var step = new PreprocessingStep { Type = PreprocessingStep.PreprocessingStepType.Imputation };

        // act
        Action act = () => step.CreateFilter(inputDimension: 2, inputSampleRate: 100.0);

        // assert
        // proves our defensive null checks inside the factory methods are working
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateFilter_Smoothing_ReturnsCorrectTypeAndMaintainsDimensions()
    {
        // arrange
        var step = new PreprocessingStep
        {
            Type = PreprocessingStep.PreprocessingStepType.Smoothing,
            Smoothing = new SmoothingOptions { Method = SmoothingOptions.SmoothingMethod.MovingAverage, WindowSize = 5 }
        };

        // act
        var (dsp, outputHz) = step.CreateFilter(inputDimension: 3, inputSampleRate: 100.0);

        // assert
        dsp.Should().BeOfType<MovingAverageSmoother>();
        dsp.InputDimension.Should().Be(3);
        dsp.OutputDimension.Should().Be(3);
        outputHz.Should().Be(100.0); // smoothing does not alter frequency
    }

    [Fact]
    public void CreateFilter_Resampling_ReturnsCorrectTypeAndAltersSampleRate()
    {
        // arrange
        var step = new PreprocessingStep
        {
            Type = PreprocessingStep.PreprocessingStepType.Resampling,
            Resampling = new ResamplingOptions { TargetHz = 25.0 }
        };

        // act
        var (dsp, outputHz) = step.CreateFilter(inputDimension: 4, inputSampleRate: 100.0);

        // assert
        dsp.Should().BeOfType<Resampler>();
        dsp.InputDimension.Should().Be(4);
        dsp.OutputDimension.Should().Be(4); // resampling does not alter features
        outputHz.Should().Be(25.0); // accurately maps the new target frequency
    }

    [Fact]
    public void CreateFilter_Reshaping_ReturnsCorrectTypeAndAltersDimensions()
    {
        // arrange
        var step = new PreprocessingStep
        {
            Type = PreprocessingStep.PreprocessingStepType.Reshaping,
            Reshaping = new ReshapingOptions { TargetDimension = 8, Method = ReshapingOptions.ReshapeMethod.TruncateOrZeroFill }
        };

        // act
        var (dsp, outputHz) = step.CreateFilter(inputDimension: 4, inputSampleRate: 100.0);

        // assert
        dsp.Should().BeOfType<Reshaper>();
        dsp.InputDimension.Should().Be(4);
        dsp.OutputDimension.Should().Be(8); // accurately maps the new target dimension
        outputHz.Should().Be(100.0);
    }

    [Fact]
    public void CreateFilter_Fft_CalculatesOutputDimensionCorrectly()
    {
        // arrange
        var step = new PreprocessingStep
        {
            Type = PreprocessingStep.PreprocessingStepType.FourierTransform,
            Fft = new FftOptions { WindowSize = 100, NumberOfFrequenciesToKeep = 10, IncludePhase = true }
        };

        // act
        var (dsp, outputHz) = step.CreateFilter(inputDimension: 2, inputSampleRate: 100.0);

        // assert
        dsp.Should().BeOfType<FftTransformer>();
        dsp.InputDimension.Should().Be(2);
        
        // 2 features * 10 freqs * 2 (mag + phase) = 40
        dsp.OutputDimension.Should().Be(40); 
        outputHz.Should().Be(100.0);
    }

    [Fact]
    public void CreateFilter_UnknownType_ThrowsInvalidOperationException()
    {
        // arrange
        var step = new PreprocessingStep
        {
            Type = (PreprocessingStep.PreprocessingStepType)999 
        };

        // act
        Action act = () => step.CreateFilter(inputDimension: 2, inputSampleRate: 100.0);

        // assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Unknown*");
    }
}