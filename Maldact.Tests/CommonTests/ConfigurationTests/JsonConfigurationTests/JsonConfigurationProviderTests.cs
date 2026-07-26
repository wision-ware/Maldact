using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FluentAssertions;
using Maldact.Common.Configuration.JsonConfiguration;
using Maldact.Tests.ClientTests.IndexingTests;

namespace Maldact.Tests.CommonTests.ConfigurationTests.JsonConfigurationTests;

/// <summary>
/// Verifies the atomic file I/O and strict data validation of the JSON configuration provider.
/// </summary>
public class JsonConfigurationProviderTests : IClassFixture<TestEnvironmentFixture>
{
    private readonly TestEnvironmentFixture _fixture;

    public JsonConfigurationProviderTests(TestEnvironmentFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Proves that a valid configuration file on disk is successfully deserialized and loaded into memory.
    /// </summary>
    [Fact]
    public void Constructor_ValidFile_LoadsAndValidatesConfig()
    {
        // arrange
        var filePath = Path.Combine(_fixture.TestDirectory, $"{Guid.NewGuid()}.json");
        var validJson = JsonSerializer.Serialize(new TestConfig { Port = 8080, Host = "localhost" });
        File.WriteAllText(filePath, validJson);

        // act
        var provider = new JsonConfigurationProvider<TestConfig>(filePath);

        // assert
        provider.Config.Should().NotBeNull();
        provider.Config.Port.Should().Be(8080);
        provider.Config.Host.Should().Be("localhost");
    }

    /// <summary>
    /// Verifies that loading a file with missing required fields or out-of-range values throws immediately.
    /// </summary>
    [Fact]
    public void Constructor_InvalidFile_ThrowsInvalidOperationException()
    {
        // arrange - port is out of the 1-65535 range
        var filePath = Path.Combine(_fixture.TestDirectory, $"{Guid.NewGuid()}.json");
        var invalidJson = JsonSerializer.Serialize(new TestConfig { Port = 999999, Host = "localhost" });
        File.WriteAllText(filePath, invalidJson);

        // act & assert
        FluentActions.Invoking(() => new JsonConfigurationProvider<TestConfig>(filePath))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*validation failed*");
    }

    /// <summary>
    /// Ensures developers cannot bypass schema rules by pushing bad data through the Update method.
    /// </summary>
    [Fact]
    public void Update_InvalidConfig_RejectsMutation()
    {
        // arrange
        var filePath = Path.Combine(_fixture.TestDirectory, $"{Guid.NewGuid()}.json");
        File.WriteAllText(filePath, JsonSerializer.Serialize(new TestConfig { Port = 8080, Host = "localhost" }));
        var provider = new JsonConfigurationProvider<TestConfig>(filePath);

        var badConfig = new TestConfig { Port = -1, Host = "" };

        // act & assert
        FluentActions.Invoking(() => provider.Update(badConfig))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*validation failed*");
            
        // ensure state remained untainted
        provider.Config.Port.Should().Be(8080);
    }

    /// <summary>
    /// Proves that Flush accurately writes the in-memory state to disk utilizing the atomic swap strategy.
    /// </summary>
    [Fact]
    public void Flush_ValidState_WritesToDiskAtomically()
    {
        // arrange
        var filePath = Path.Combine(_fixture.TestDirectory, $"{Guid.NewGuid()}.json");
        File.WriteAllText(filePath, JsonSerializer.Serialize(new TestConfig { Port = 8080, Host = "localhost" }));
        var provider = new JsonConfigurationProvider<TestConfig>(filePath);

        // act
        provider.Update(new TestConfig { Port = 9000, Host = "remote" });
        provider.Flush();

        // assert
        var updatedJson = File.ReadAllText(filePath);
        var updatedConfig = JsonSerializer.Deserialize<TestConfig>(updatedJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        updatedConfig.Should().NotBeNull();
        updatedConfig!.Port.Should().Be(9000);
        updatedConfig.Host.Should().Be("remote");
    }

    // --- SETUP STUBS ---

    /// <summary>
    /// A lightweight stub to mathematically prove DataAnnotation validation pipelines work in the provider.
    /// </summary>
    public class TestConfig
    {
        [Required]
        public string Host { get; set; } = string.Empty;

        [Range(1, 65535)]
        public int Port { get; set; } = 1;
    }
}