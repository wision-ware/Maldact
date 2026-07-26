using System.Collections.Concurrent;
using FluentAssertions;
using Maldact.Backend.Server.Authentication;
using Maldact.Core.Server;
using Maldact.Core.Server.Authentication;

namespace Maldact.Tests.BackendTests.ServerTests.AuthenticationTests;

/// <summary>
/// Verifies the startup defensive checks, token routing, and concurrent safety of the authentication primitives.
/// </summary>
public class AuthenticatorTests
{
    private readonly AuthToken _userToken1 = new("user-token-1");
    private readonly AuthToken _userToken2 = new("user-token-2");
    private readonly AuthToken _adminToken1 = new("admin-token-1");
    
    [Fact]
    public void Constructor_NullTokenSets_ThrowsArgumentNullException()
    {
        // arrange
        var validSet = new HashSet<AuthToken>();

        // act & assert
        FluentActions.Invoking(() => new Authenticator(null!, validSet))
            .Should().Throw<ArgumentNullException>();

        FluentActions.Invoking(() => new Authenticator(validSet, null!))
            .Should().Throw<ArgumentNullException>();
    }
    
    [Fact]
    public void Constructor_OverlappingTokens_ThrowsInvalidOperationException()
    {
        // arrange
        var userSet = new HashSet<AuthToken> { _userToken1 };
        var adminSet = new HashSet<AuthToken> { _userToken1 }; // overlap!

        // act & assert
        FluentActions.Invoking(() => new Authenticator(userSet, adminSet))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*ambiguously mapped to multiple roles*");
    }
    
    [Fact]
    public void Authenticate_ValidUserToken_ReturnsTrueAndUserPass()
    {
        // arrange
        var authenticator = CreateValidAuthenticator();

        // act
        var isValid = authenticator.Authenticate(_userToken1, out var pass);

        // assert
        isValid.Should().BeTrue();
        pass.Should().NotBeNull();
        pass!.AccessRole.Should().Be(AccessPass.Role.User);
    }
    
    [Fact]
    public void Authenticate_ValidAdminToken_ReturnsTrueAndAdminPass()
    {
        // arrange
        var authenticator = CreateValidAuthenticator();

        // act
        var isValid = authenticator.Authenticate(_adminToken1, out var pass);

        // assert
        isValid.Should().BeTrue();
        pass.Should().NotBeNull();
        pass!.AccessRole.Should().Be(AccessPass.Role.Admin);
    }
    
    [Fact]
    public void Authenticate_UnknownToken_ReturnsFalseAndNullPass()
    {
        // arrange
        var authenticator = CreateValidAuthenticator();
        var fakeToken = new AuthToken("malicious-token");

        // act
        var isValid = authenticator.Authenticate(fakeToken, out var pass);

        // assert
        isValid.Should().BeFalse();
        pass.Should().BeNull();
    }
    
    [Fact]
    public void AccessPass_Equality_IsBasedOnIdAndIdentity()
    {
        // arrange
        var pass1 = new AccessPass(AccessPass.Role.User);
        var pass2 = new AccessPass(AccessPass.Role.User);

        // act & assert
        pass1.Should().NotBe(pass2, "each instantiation generates a unique internal Guid PassId.");
        pass1.Should().Be(pass1);
        pass1.GetHashCode().Should().NotBe(pass2.GetHashCode());
    }
    
    [Fact]
    public async Task SessionId_Generation_IsThreadSafeUnderLoad()
    {
        // arrange
        const int concurrentRequests = 10_000;
        var generatedIds = new ConcurrentBag<SessionId>();

        // act: blast the constructor across the thread pool
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ => Task.Run(() => generatedIds.Add(new SessionId())));
        
        await Task.WhenAll(tasks);

        // assert: if the counter wasn't atomic, duplicates would collapse into a smaller distinct set
        var distinctIds = generatedIds.Select(id => id.Value).Distinct().ToList();
        
        distinctIds.Should().HaveCount(concurrentRequests, "Interlocked.Increment guarantees no dropped or duplicated identifiers.");
    }
    
    /// <summary>
    /// Generates a correctly configured authenticator for state tests.
    /// </summary>
    private Authenticator CreateValidAuthenticator()
    {
        var users = new HashSet<AuthToken> { _userToken1, _userToken2 };
        var admins = new HashSet<AuthToken> { _adminToken1 };
        
        return new Authenticator(users, admins);
    }
}