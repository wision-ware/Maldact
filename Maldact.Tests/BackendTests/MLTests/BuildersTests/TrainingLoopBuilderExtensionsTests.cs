using FluentAssertions;
using Maldact.Backend.ML.Builders;
using Maldact.Backend.ML.Training.Loops;
using Maldact.Core.Config.ConfigDefinitions;
using TorchSharp;

namespace Maldact.Tests.BackendTests.MLTests.BuildersTests;

/// <summary>
/// Verifies the defensive routing, instantiation, and property binding of the training loop builder.
/// </summary>
public class TrainingLoopBuilderExtensionsTests
{
    [Fact]
    public void BuildLoop_NullParameters_ThrowsArgumentNullException()
    {
        // arrange
        var validConfig = new TrainingConfiguration();
        var validSpec = new ModelSpecification { Algorithm = ModelSpecification.AlgorithmType.Gru };

        // act & assert
        FluentActions.Invoking(() => ((TrainingConfiguration)null!).BuildLoop(validSpec))
            .Should().Throw<ArgumentNullException>();

        FluentActions.Invoking(() => validConfig.BuildLoop(null!))
            .Should().Throw<ArgumentNullException>();
    }
    
    [Theory]
    [InlineData(ModelSpecification.AlgorithmType.Gru)]
    [InlineData(ModelSpecification.AlgorithmType.Cnn)]
    [InlineData(ModelSpecification.AlgorithmType.TreeEnsemble)]
    public void BuildLoop_MissingNestedSpecification_ThrowsInvalidOperationException(ModelSpecification.AlgorithmType type)
    {
        // arrange
        var config = new TrainingConfiguration();
        var spec = new ModelSpecification { Algorithm = type }; // nested params (Gru/Cnn/Tree) intentionally left null

        // act & assert
        FluentActions.Invoking(() => config.BuildLoop(spec))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*specification must be provided*");
    }
    
    [Fact]
    public void BuildLoop_ValidGruSpecification_ReturnsGruTrainingLoop()
    {
        // arrange
        var config = CreateBaseConfig();
        var spec = new ModelSpecification
        {
            Algorithm = ModelSpecification.AlgorithmType.Gru,
            InputDimension = 10,
            OutputDimension = 2,
            Gru = new GruParameters { HiddenSize = 64, NumLayers = 2, Dropout = 0.2 }
        };

        // act
        var result = config.BuildLoop(spec);

        // assert
        result.Should().BeOfType<GruTrainingLoop>();
        var gruLoop = (GruTrainingLoop)result;
        
        gruLoop.MaxEpochs.Should().Be(config.MaxEpochs);
        gruLoop.LearningRate.Should().Be(config.LearningRate);
        gruLoop.Patience.Should().Be(config.Patience);
    }
    
    [Fact]
    public void BuildLoop_ValidCnnSpecification_ReturnsCnnTrainingLoop()
    {
        // arrange
        var config = CreateBaseConfig();
        var spec = new ModelSpecification
        {
            Algorithm = ModelSpecification.AlgorithmType.Cnn,
            InputDimension = 10,
            OutputDimension = 2,
            Cnn = new CnnParameters { ChannelSizes = new[] { 16, 32 }, KernelSize = 3, Stride = 1 }
        };

        // act
        var result = config.BuildLoop(spec);

        // assert
        result.Should().BeOfType<CnnTrainingLoop>();
        var cnnLoop = (CnnTrainingLoop)result;
        
        cnnLoop.MaxEpochs.Should().Be(config.MaxEpochs);
        cnnLoop.LearningRate.Should().Be(config.LearningRate);
        cnnLoop.Patience.Should().Be(config.Patience);
    }
    
    [Fact]
    public void BuildLoop_ValidTreeSpecification_ReturnsTreeTrainingLoop()
    {
        // arrange
        var config = CreateBaseConfig();
        var spec = new ModelSpecification
        {
            Algorithm = ModelSpecification.AlgorithmType.TreeEnsemble,
            OutputDimension = 2,
            Tree = new TreeParameters 
            { 
                EnsembleType = TreeParameters.TreeType.RandomForest, 
                NumberOfTrees = 100, 
                MaxDepth = 6 
            }
        };

        // act
        var result = config.BuildLoop(spec);

        // assert
        result.Should().BeOfType<TreeTrainingLoop>();
        var treeLoop = (TreeTrainingLoop)result;
        
        // assumes TreeTrainingLoop.TreeType enum mapping
        treeLoop.EnsembleType.Should().Be(TreeTrainingLoop.TreeType.RandomForest);
        treeLoop.NumberOfTrees.Should().Be(100);
        treeLoop.MaxDepth.Should().Be(6);
        treeLoop.NumClasses.Should().Be(2);
    }
    
    [Fact]
    public void BuildLoop_TreeWithNullEnsembleType_ThrowsInvalidOperationException()
    {
        // arrange
        var config = CreateBaseConfig();
        var spec = new ModelSpecification
        {
            Algorithm = ModelSpecification.AlgorithmType.TreeEnsemble,
            Tree = new TreeParameters { EnsembleType = null } 
        };

        // act & assert
        FluentActions.Invoking(() => config.BuildLoop(spec))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*explicitly specified*");
    }
    
    [Fact]
    public void BuildLoop_UnsupportedAlgorithm_ThrowsNotSupportedException()
    {
        // arrange
        var config = CreateBaseConfig();
        var spec = new ModelSpecification { Algorithm = (ModelSpecification.AlgorithmType)999 };

        // act & assert
        FluentActions.Invoking(() => config.BuildLoop(spec))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*not supported for training loops*");
    }
    
    private static TrainingConfiguration CreateBaseConfig() => new()
    {
        Device = TrainingConfiguration.DeviceType.Cpu,
        MaxEpochs = 50,
        LearningRate = 0.01,
        Patience = 5,
        RandomSeed = 42
    };
}