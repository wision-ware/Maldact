using FluentAssertions;
using Maldact.Backend.Server.Builders;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.Server.Authentication;

namespace Maldact.Tests.BackendTests.ServerTests.BuildersTests;

/// <summary>
/// Verifies the authenticator builder
/// </summary>
public class AuthenticatorBuilderExtensionsTests
{
   
    [Fact]
    public void BuildAuthenticator_NullConfiguration_ThrowsArgumentNullException()
    {
        // arrange
        ServerConfiguration? nullConfig = null;

        // act & assert
        FluentActions.Invoking(() => nullConfig!.BuildAuthenticator())
            .Should().Throw<ArgumentNullException>();
    }
    
    [Fact]
    public void BuildAuthenticator_NullCollections_GracefullyBuildsEmptyAuthenticator()
    {
        // arrange
        var config = new ServerConfiguration
        {
            AdminKeys = null!, // forced bypass of the init property default
            UserKeys = null!
        };

        // act
        var authenticator = config.BuildAuthenticator();

        // assert
        authenticator.Should().NotBeNull();
        
        // verify it successfully resolves to misses rather than crashing
        var result = authenticator.Authenticate(new AuthToken("any-token"), out var pass);
        result.Should().BeFalse();
        pass.Should().BeNull();
    }
    
    [Fact]
    public void BuildAuthenticator_ValidConfiguration_MapsTokensToRolesSuccessfully()
    {
        // arrange
        var config = new ServerConfiguration
        {
            AdminKeys = new HashSet<string> { "admin-1" },
            UserKeys = new HashSet<string> { "user-1" }
        };

        // act
        var authenticator = config.BuildAuthenticator();

        // assert
        authenticator.Authenticate(new AuthToken("admin-1"), out var adminPass).Should().BeTrue();
        adminPass!.AccessRole.Should().Be(AccessPass.Role.Admin);

        authenticator.Authenticate(new AuthToken("user-1"), out var userPass).Should().BeTrue();
        userPass!.AccessRole.Should().Be(AccessPass.Role.User);
    }
}