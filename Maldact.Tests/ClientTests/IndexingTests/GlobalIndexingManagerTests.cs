using FluentAssertions;
using Maldact.Client.Indexing;
using Maldact.Core.Server.Authentication;

namespace Maldact.Tests.ClientTests.IndexingTests;

/// <summary>
/// Verifies the safe local storage manipulation and environment isolation of the global state managers.
/// </summary>
[Collection("Integration")] // prevents concurrent I/O collisions if multiple test classes hit the manager
public class GlobalIndexingManagerTests : IClassFixture<TestEnvironmentFixture>
{
    private readonly TestEnvironmentFixture _fixture;

    public GlobalIndexingManagerTests(TestEnvironmentFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Fact]
    public void ConfigurationIndexManager_SetAndGetState_RoundTripsSuccessfully()
    {
        // arrange
        var expectedState = new ConfigurationIndex(
            ServerConfigurationPath: "path/to/server.json",
            PreprocessingContractPath: "path/to/prep.json",
            TrainingConfigurationPath: "path/to/train.json",
            ModelSpecificationPath: "path/to/model.json"
        );

        // act
        GlobalConfigurationIndexManager.SetNewState(expectedState);
        var retrievedState = GlobalConfigurationIndexManager.GetCurrentState();

        // assert
        retrievedState.Should().NotBeNull();
        retrievedState.Should().BeEquivalentTo(expectedState);
    }
    
    [Fact]
    public void ConfigurationIndexManager_SetNullState_ClearsFile()
    {
        // arrange
        GlobalConfigurationIndexManager.SetNewState(new ConfigurationIndex("a", "b", "c", "d"));
        GlobalConfigurationIndexManager.GetCurrentState().Should().NotBeNull(); // verify it exists

        // act
        GlobalConfigurationIndexManager.SetNewState(null);

        // assert
        GlobalConfigurationIndexManager.GetCurrentState().Should().BeNull("passing null must purge the file from disk.");
    }
    
    [Fact]
    public void ConnectionStateIndexManager_SetAndGetState_RoundTripsSuccessfully()
    {
        // arrange
        var expectedState = new ConnectionStateIndex
        {
            Host = "192.168.1.100",
            Port = 5000,
            AuthToken = new AuthToken("test-token-123")
        };

        // act
        GlobalConnectionStateIndexManager.SetNewState(expectedState);
        var retrievedState = GlobalConnectionStateIndexManager.GetCurrentState();

        // assert
        retrievedState.Should().NotBeNull();
        retrievedState.Should().BeEquivalentTo(expectedState);
    }
}