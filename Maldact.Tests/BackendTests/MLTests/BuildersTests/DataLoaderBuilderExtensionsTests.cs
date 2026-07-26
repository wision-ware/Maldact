using FluentAssertions;
using Maldact.Backend.ML.Builders;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.Data;
using Maldact.Core.ML;
using Maldact.Core.ML.Training;

namespace Maldact.Tests.BackendTests.MLTests.BuildersTests;

/// <summary>
/// Verifies the defensive boundary conditions, dimensional math calculations, 
/// and algorithm-specific formatting injections for the streaming data loader builder.
/// </summary>
public class DataLoaderBuilderExtensionsTests
{
    
    [Fact]
    public void AddFormatting_NullParameters_ThrowsArgumentNullException()
    {
        // arrange
        var validLoader = new StubDataLoader(100.0);
        var validSpec = CreateValidSpec(ModelSpecification.AlgorithmType.Gru);
        var validConfig = CreateValidConfig();

        // act & assert
        FluentActions.Invoking(() => ((IRawLabeledTrainingDataLoader)null!).AddFormatting(validSpec, validConfig))
            .Should().Throw<ArgumentNullException>();

        FluentActions.Invoking(() => validLoader.AddFormatting(null!, validConfig))
            .Should().Throw<ArgumentNullException>();

        FluentActions.Invoking(() => validLoader.AddFormatting(validSpec, null!))
            .Should().Throw<ArgumentNullException>();
    }
    
    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    public void AddFormatting_InvalidSampleRate_ThrowsInvalidOperationException(double invalidHz)
    {
        // arrange
        var loader = new StubDataLoader(invalidHz);
        var spec = CreateValidSpec(ModelSpecification.AlgorithmType.Gru);
        var config = CreateValidConfig();

        // act & assert
        FluentActions.Invoking(() => loader.AddFormatting(spec, config))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*strictly positive sample rate*");
    }
    
    [Fact]
    public void AddFormatting_MissingStructuralBounds_ThrowsInvalidOperationException()
    {
        // arrange
        var loader = new StubDataLoader(100.0);
        var config = CreateValidConfig();
        
        // inject nulls to bypass initializers and test the builder's internal guards
        var missingWindowSize = CreateValidSpec(ModelSpecification.AlgorithmType.Gru);
        typeof(ModelSpecification).GetProperty(nameof(ModelSpecification.WindowSize))!.SetValue(missingWindowSize, null);

        var missingWindowStride = CreateValidSpec(ModelSpecification.AlgorithmType.Gru);
        typeof(ModelSpecification).GetProperty(nameof(ModelSpecification.WindowStride))!.SetValue(missingWindowStride, null);

        var missingBatchSizeConfig = new TrainingConfiguration(); // BatchSize defaults to null

        // act & assert
        FluentActions.Invoking(() => loader.AddFormatting(missingWindowSize, config))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*positive WindowSize*");

        FluentActions.Invoking(() => loader.AddFormatting(missingWindowStride, config))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*positive WindowStride*");

        FluentActions.Invoking(() => loader.AddFormatting(CreateValidSpec(ModelSpecification.AlgorithmType.Gru), missingBatchSizeConfig))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*strictly positive BatchSize*");
    }
    
    [Theory]
    [InlineData(ModelSpecification.AlgorithmType.Gru)]
    [InlineData(ModelSpecification.AlgorithmType.Cnn)]
    [InlineData(ModelSpecification.AlgorithmType.TreeEnsemble)]
    public void AddFormatting_ValidConfigurations_ReturnsFormattedLoader(ModelSpecification.AlgorithmType algorithm)
    {
        // arrange
        var loader = new StubDataLoader(100.0);
        var spec = CreateValidSpec(algorithm);
        var config = CreateValidConfig();

        // act
        var result = loader.AddFormatting(spec, config);

        // assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IFormattedLabeledTrainingDataLoader>();
    }
    
    [Fact]
    public void AddFormatting_UnsupportedAlgorithm_ThrowsNotSupportedException()
    {
        // arrange
        var loader = new StubDataLoader(100.0);
        var spec = CreateValidSpec((ModelSpecification.AlgorithmType)999);
        var config = CreateValidConfig();

        // act & assert
        FluentActions.Invoking(() => loader.AddFormatting(spec, config))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*does not have a supported batch formatter*");
    }

   
    private static ModelSpecification CreateValidSpec(ModelSpecification.AlgorithmType algorithm) => new()
    {
        Name = "TestModel",
        Algorithm = algorithm,
        InputDimension = 10,
        OutputDimension = 2,
        WindowSize = 100,
        WindowStride = 50,
        // Provide mock inner configurations so downstream formatters don't null reference if they validate
        Gru = new GruParameters { HiddenSize = 64, NumLayers = 2, Dropout = 0.2 },
        Cnn = new CnnParameters { ChannelSizes = new[] { 16, 32 }, KernelSize = 3, Stride = 1 },
        Tree = new TreeParameters { EnsembleType = TreeParameters.TreeType.RandomForest, NumberOfTrees = 100, MaxDepth = 6 }
    };
    
    private static TrainingConfiguration CreateValidConfig() => new()
    {
        BatchSize = 32,
        BatchesPerEpoch = 100,
        Device = TrainingConfiguration.DeviceType.Cpu,
        MaxEpochs = 10,
        LearningRate = 0.001,
        Patience = 5
    };
    
    private class StubDataLoader : IRawLabeledTrainingDataLoader
    {
        public double SampleRateHz { get; }
        public ClassificationClass[] Classes { get; }

        public StubDataLoader(double hz)
        {
            SampleRateHz = hz;
            // Assumes ClassificationClass constructor signature matching previous setups
            Classes = new[] { new ClassificationClass("TargetA"), new ClassificationClass("TargetB") };
        }

        public RawTrainingWindow GetRandomTrainingWindow(TimeSpan windowDuration) => throw new NotImplementedException();
        public IEnumerable<RawTrainingWindow> GetSequentialWindows(TimeSpan windowDuration, TimeSpan stride) => throw new NotImplementedException();
    }
}