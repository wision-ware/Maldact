using FluentAssertions;
using Maldact.Backend.ML.Builders;
using Maldact.Backend.ML.Modules;
using Maldact.Core.Config.ConfigDefinitions;

namespace Maldact.Tests.BackendTests.MLTests.BuildersTests;

/// <summary>
/// Verifies the defensive routing and instantiation of TorchSharp native modules.
/// </summary>
public class TorchModuleBuilderExtensionsTests
{
    private const string DefaultName = "TestModule";
    private const int ValidInput = 10;
    private const int ValidOutput = 2;

    [Fact]
    public void BuildModule_Cnn_NullConfiguration_ThrowsArgumentNullException()
    {
        // arrange
        CnnParameters? config = null;

        // act & assert
        FluentActions.Invoking(() => config!.BuildModule(DefaultName, ValidInput, ValidOutput))
            .Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0, ValidOutput)]
    [InlineData(ValidInput, 0)]
    [InlineData(-1, ValidOutput)]
    [InlineData(ValidInput, -1)]
    public void BuildModule_Cnn_InvalidDimensions_ThrowsArgumentOutOfRangeException(int input, int output)
    {
        // arrange
        var config = new CnnParameters { ChannelSizes = new[] { 16, 32 }, KernelSize = 3, Stride = 1 };

        // act & assert
        FluentActions.Invoking(() => config.BuildModule(DefaultName, input, output))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*dimension must be positive*");
    }

    [Fact]
    public void BuildModule_Cnn_NullChannelSizes_ThrowsArgumentException()
    {
        // arrange
        var config = new CnnParameters { ChannelSizes = null!, KernelSize = 3, Stride = 1 };

        // act & assert
        FluentActions.Invoking(() => config.BuildModule(DefaultName, ValidInput, ValidOutput))
            .Should().Throw<ArgumentException>()
            .WithMessage("*explicitly defined*");
    }

    [Fact]
    public void BuildModule_Cnn_ValidConfiguration_InstantiatesSuccessfully()
    {
        // arrange
        var config = new CnnParameters 
        { 
            ChannelSizes = new[] { 16, 32 }, 
            KernelSize = 3, 
            Stride = 1 
        };

        // act
        var result = config.BuildModule("CustomCnn", ValidInput, ValidOutput);

        // assert
        result.Should().NotBeNull();
        result.Should().BeOfType<TimeSeriesCnn>();
        result.GetName().Should().Be("CustomCnn", "the module name should be explicitly passed to the Torch graph.");
    }

    [Fact]
    public void BuildModule_Gru_NullConfiguration_ThrowsArgumentNullException()
    {
        // arrange
        GruParameters? config = null;

        // act & assert
        FluentActions.Invoking(() => config!.BuildModule(DefaultName, ValidInput, ValidOutput))
            .Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0, ValidOutput)]
    [InlineData(ValidInput, 0)]
    [InlineData(-1, ValidOutput)]
    [InlineData(ValidInput, -1)]
    public void BuildModule_Gru_InvalidDimensions_ThrowsArgumentOutOfRangeException(int input, int output)
    {
        // arrange
        var config = new GruParameters { HiddenSize = 64, NumLayers = 2, Dropout = 0.2 };

        // act & assert
        FluentActions.Invoking(() => config.BuildModule(DefaultName, input, output))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*dimension must be positive*");
    }

    [Fact]
    public void BuildModule_Gru_MissingOptionalParameters_UsesArchitecturalDefaults()
    {
        // arrange
        var config = new GruParameters 
        { 
            HiddenSize = null, // Should default to 64
            NumLayers = 2, 
            Dropout = 0.2 
        };

        // act
        var result = config.BuildModule("CustomGru", ValidInput, ValidOutput);

        // assert
        result.Should().NotBeNull();
        result.Should().BeOfType<TimeSeriesGru>();
        result.GetName().Should().Be("CustomGru");
    }
}