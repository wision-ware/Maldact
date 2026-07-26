using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Maldact.Backend.Diagnostics;
using Maldact.Backend.Server;
using Maldact.Backend.Server.Streaming;
using Maldact.Core.ML.Inference;
using Maldact.Core.Results;
using Maldact.Core.Server.Authentication;

namespace Maldact.Tests.BackendTests.ServerTests;

public class SessionContextTests
{
    
    [Fact]
    public void Constructor_NullDependencies_ThrowsArgumentNullException()
    {
        // arrange
        var auth = new StubAuthenticator();
        var sm = CreateDummyStreamingManager();
        SessionContext.SlotTryGetter getter = (AccessPass p, out StreamingSlot? s) => { s = null; return false; };
        SessionContext.SlotTryBooker booker = (AccessPass p, out StreamingSlot? s) => { s = null; return false; };
        Func<ServerDiagnosticMetrics?> diag = () => null;
        using var cts = new CancellationTokenSource();

        // act & assert
        FluentActions.Invoking(() => new SessionContext(null!, sm, getter, booker, diag, cts)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SessionContext(auth, null!, getter, booker, diag, cts)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SessionContext(auth, sm, null!, booker, diag, cts)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SessionContext(auth, sm, getter, null!, diag, cts)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SessionContext(auth, sm, getter, booker, null!, cts)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SessionContext(auth, sm, getter, booker, diag, null!)).Should().Throw<ArgumentNullException>();
    }
    
    [Fact]
    public void SetAuthenticatedState_ValidPass_LocksInClaims()
    {
        // arrange
        var context = CreateValidContext();
        var pass = new AccessPass(AccessPass.Role.User);

        // act
        context.IsAuthenticated.Should().BeFalse();
        context.SetAuthenticatedState(pass);

        // assert
        context.IsAuthenticated.Should().BeTrue();
        context.AccessPass.Should().Be(pass);

        // secondary mutation attempt must violently fail
        FluentActions.Invoking(() => context.SetAuthenticatedState(new AccessPass(AccessPass.Role.Admin)))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*already authenticated*");
    }
    
    [Fact]
    public void RequestServerShutdown_UserRole_ThrowsUnauthorizedAccessException()
    {
        // arrange
        var context = CreateValidContext();
        context.SetAuthenticatedState(new AccessPass(AccessPass.Role.User));

        // act & assert
        FluentActions.Invoking(() => context.RequestServerShutdown())
            .Should().Throw<UnauthorizedAccessException>()
            .WithMessage("*Only administrators*");
            
        context.ServerCancellationToken.IsCancellationRequested.Should().BeFalse();
    }
    
    [Fact]
    public async Task RequestServerShutdown_AdminRole_TriggersCancellation()
    {
        // arrange
        var context = CreateValidContext();
        context.SetAuthenticatedState(new AccessPass(AccessPass.Role.Admin));

        // act
        context.RequestServerShutdown();
        await Task.Delay(600); // wait for cancellation signal to be sent

        // assert
        context.ServerCancellationToken.IsCancellationRequested.Should().BeTrue("the administrator successfully fired the global teardown token.");
    }
    

    private static SessionContext CreateValidContext()
    {
        var auth = new StubAuthenticator();
        var sm = CreateDummyStreamingManager();
        SessionContext.SlotTryGetter getter = (AccessPass p, out StreamingSlot? s) => { s = null; return false; };
        SessionContext.SlotTryBooker booker = (AccessPass p, out StreamingSlot? s) => { s = null; return false; };
        Func<ServerDiagnosticMetrics?> diag = () => null;
        var cts = new CancellationTokenSource();

        return new SessionContext(auth, sm, getter, booker, diag, cts);
    }
    
    private static StreamingManager CreateDummyStreamingManager()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=MaldactTest", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var transient = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
        var cert = new X509Certificate2(transient.Export(X509ContentType.Pfx));

        return new StreamingManager(listener, new StubInferenceFactory(), cert);
    }

    private class StubAuthenticator : IAuthenticator
    {
        public bool Authenticate(AuthToken token, out AccessPass? pass) { pass = null; return false; }
    }

    private class StubInferenceFactory : IInferenceEngineFactory
    {
        public IInferenceEngine Create(StreamTime sessionStartTime) => throw new NotImplementedException();
    }
}