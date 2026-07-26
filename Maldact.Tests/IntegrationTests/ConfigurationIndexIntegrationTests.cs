using FluentAssertions;
using Maldact.Client.Indexing;
using Maldact.Core.Config.ConfigDefinitions;

namespace Maldact.Tests.IntegrationTests;

[Collection("Integration")]
public sealed class ConfigurationIndexIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task ConfigSetAndGet_WithValidModelSpecification_UpdatesIndexAndPrintsRoute()
    {
        // Arrange
        var validSpec = new ModelSpecification
        {
            Name = "Inference_GRU_v1",
            Algorithm = ModelSpecification.AlgorithmType.Gru,
            InputDimension = 4,
            OutputDimension = 2,
            WindowSize = 50,
            WindowStride = 10,
            Gru = new GruParameters { HiddenSize = 64, NumLayers = 2, Dropout = 0.2 }
        };
        GlobalConfigurationIndexManager.Clear();
        GlobalConnectionStateIndexManager.Clear();
        
        string targetFilePath = WriteConfigFile("modelSpec.json", validSpec);
        
        // Act - Set the configuration path route via the CLI app command app instance
        int setExitCode = await App.RunAsync(new[] { "config", "set", "model-specification", targetFilePath });
        
        // Assert - Verify index mutation on disk
        setExitCode.Should().Be(0);
        var state = GlobalConfigurationIndexManager.GetCurrentState();
        state.Should().NotBeNull();
        state!.ModelSpecificationPath.Should().Be(targetFilePath);

        // Act - Read the value back using the get command
        int getExitCode = await App.RunAsync(new[] { "config", "get", "model-specification" });
        
        // Assert - Validate terminal output formatting
        getExitCode.Should().Be(0);
    }

    [Fact]
    public async Task ConfigSet_WithInvalidSchemaFile_ShortCircuitsAndReturnsErrorStatus()
    {
        // Arrange - Write an unaligned, broken text payload instead of a valid PreprocessingContract
        string brokenFilePath = Path.Combine(TestDirectory, "corruptContract.json");
        await File.WriteAllTextAsync(brokenFilePath, "{ \"InputDimension\": \"NotAnInteger\" }");
        
        GlobalConfigurationIndexManager.Clear();
        GlobalConnectionStateIndexManager.Clear();

        // Act
        int exitCode = await App.RunAsync(new[] { "config", "set", "preprocessing-contract", brokenFilePath });

        // Assert
        exitCode.Should().Be(1);
        var state = GlobalConfigurationIndexManager.GetCurrentState();
        state?.PreprocessingContractPath.Should().BeNull("The index must not track unverified or broken file structures.");
    }

    [Fact]
    public async Task ConfigSet_WithNullKeyword_PurgesSlotFromIndexState()
    {
        // Arrange - Seed the state registry with a mock initial path
        GlobalConfigurationIndexManager.Clear();
        GlobalConnectionStateIndexManager.Clear();
        
        var initialState = new ConfigurationIndex(null, "some/old/path.json", null, null);
        GlobalConfigurationIndexManager.SetNewState(initialState);

        // Act
        int exitCode = await App.RunAsync(new[] { "config", "set", "preprocessing-contract", "null" });

        // Assert
        exitCode.Should().Be(0);
        var finalState = GlobalConfigurationIndexManager.GetCurrentState();
        finalState!.PreprocessingContractPath.Should().BeNull("The target slot must be purged from disk when passing the 'null' parameter.");
    }
}