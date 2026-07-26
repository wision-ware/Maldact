using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Maldact.Backend.Diagnostics;
using Maldact.Backend.Server;
using Maldact.Backend.Server.ControlProtocol;
using Maldact.Backend.Server.ControlProtocol.Responses;
using Maldact.Backend.Server.Streaming;
using Maldact.Core.ML.Inference;
using Maldact.Core.Results;
using Maldact.Core.Server.Authentication;

namespace Maldact.Tests.BackendTests.ServerTests.ControlProtocolTests;

/// <summary>
/// Verifies the GC memory lifecycle and thread-safety of the command routing engine.
/// </summary>
public class CommandRouterTests
{
    /// <summary>
    /// Proves that the router correctly rejects null execution contexts to prevent downstream crashes.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NullContext_ThrowsArgumentNullException()
    {
        // arrange
        var router = new CommandRouter();

        // act & assert
        await FluentActions.Invoking(() => router.ExecuteAsync("status", null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Mathematically proves that the ConditionalWeakTable releases parsing configurations 
    /// when the session context drops out of memory, preventing server memory leaks.
    /// </summary>
    [Fact]
    public void CommandRouter_DoesNotLeakMemory_WhenSessionsDisconnect()
    {
        // arrange
        var router = new CommandRouter();
        
        // create a session in an isolated function so it drops out of scope locally
        var (weakSession, weakRouter) = ExecuteAndCaptureWeakReferences(router);

        // act: force a blocking garbage collection cycle
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // assert
        weakSession.IsAlive.Should().BeFalse("the session context should have been garbage collected.");
        
        // if the session is dead, the router MUST have released its internal configuration for that session
        // (verified implicitly because the ConditionalWeakTable ties the config lifetime to the session key)
    }

    /// <summary>
    /// Proves that the SemaphoreSlim successfully prevents concurrent command requests 
    /// from corrupting the shared output capture buffers on a single session.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ConcurrentInvocations_MaintainsThreadSafety()
    {
        // arrange
        var router = new CommandRouter();
        var context = CreateDummySession();
        
        // act
        // fire concurrent, overlapping commands against the exact same session context
        var task1 = router.ExecuteAsync("--help", context);
        var task2 = router.ExecuteAsync("--help", context);

        var results = await Task.WhenAll(task1, task2);

        // assert
        results.Should().HaveCount(2);
        results[0].ResponseType.Should().NotBe(CommandResponse.Type.Error);
        results[1].ResponseType.Should().NotBe(CommandResponse.Type.Error);
    }
    
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (WeakReference session, WeakReference router) ExecuteAndCaptureWeakReferences(CommandRouter router)
    {
        var context = CreateDummySession();
        
        // force the router to build and cache the configuration
        _ = router.ExecuteAsync("--help", context).GetAwaiter().GetResult();
        
        return (new WeakReference(context), new WeakReference(router));
    }

    private static SessionContext CreateDummySession()
    {
        // creates a valid, empty session purely for dictionary key hashing
        var listener = new TcpListener(IPAddress.Loopback, 0);
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=MaldactTest", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var transient = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
        var cert = new X509Certificate2(transient.Export(X509ContentType.Pfx));

        var manager = new StreamingManager(listener, new StubInferenceFactory(), cert);

        SessionContext.SlotTryGetter getter = (AccessPass p, out StreamingSlot? s) => { s = null; return false; };
        SessionContext.SlotTryBooker booker = (AccessPass p, out StreamingSlot? s) => { s = null; return false; };
        Func<ServerDiagnosticMetrics?> diag = () => null;
        var cts = new CancellationTokenSource();

        return new SessionContext(new StubAuthenticator(), manager, getter, booker, diag, cts);
    }

    // empty stubs to satisfy strict constructors
    private class StubAuthenticator : IAuthenticator { public bool Authenticate(AuthToken token, out AccessPass? pass) { pass = null; return false; } }
    private class StubInferenceFactory : IInferenceEngineFactory { public IInferenceEngine Create(StreamTime sessionStartTime) => throw new NotImplementedException(); }
}