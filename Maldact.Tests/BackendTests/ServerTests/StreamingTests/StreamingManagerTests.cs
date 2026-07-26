using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Maldact.Backend.Server.Streaming;
using Maldact.Core.ML.Inference;
using Maldact.Core.Results;

namespace Maldact.Tests.BackendTests.ServerTests.StreamingTests;

public class StreamingManagerTests
{
    /// <summary>
    /// Ensures defensive instantiation rejects missing network dependencies.
    /// </summary>
    [Fact]
    public void StreamingManager_NullDependencies_ThrowsArgumentNullException()
    {
        // arrange
        var listener = new TcpListener(IPAddress.Loopback, 0);
        var factory = new StubInferenceFactory();
        using var cert = GenerateTransientCert();

        // act & assert
        FluentActions.Invoking(() => new StreamingManager(null!, factory, cert)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new StreamingManager(listener, null!, cert)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new StreamingManager(listener, factory, null!)).Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that reserved slots are properly indexed and return a valid routing token.
    /// </summary>
    [Fact]
    public void RegisterPendingStream_ValidSlot_ReturnsGuidToken()
    {
        // arrange
        var listener = new TcpListener(IPAddress.Loopback, 0);
        using var cert = GenerateTransientCert();
        var manager = new StreamingManager(listener, new StubInferenceFactory(), cert);
        
        var slot = new StreamingSlot(new StubResultRepository());

        // act
        var token = manager.RegisterPendingStream(slot);

        // assert
        Guid.TryParse(token, out _).Should().BeTrue("the routing token must be a serialized GUID to match the header protocol.");
    }

    /// <summary>
    /// Proves that the underlying TCP accept loop cleanly aborts when the server issues a global shutdown token.
    /// </summary>
    [Fact]
    public async Task StartAsync_CancellationRequested_GracefullyTerminatesLoop()
    {
        // arrange
        var listener = new TcpListener(IPAddress.Loopback, 0);
        using var cert = GenerateTransientCert();
        var manager = new StreamingManager(listener, new StubInferenceFactory(), cert);
        
        using var cts = new CancellationTokenSource();

        // act
        var serverTask = manager.StartAsync(cts.Token);
        
        // signal immediate teardown
        await cts.CancelAsync();
        await serverTask;

        // assert
        serverTask.IsCompletedSuccessfully.Should().BeTrue("the loop must catch the TaskCanceledException and exit gracefully without faulting.");
    }

    // --- SETUP HELPERS & STUBS ---

    /// <summary>
    /// Generates a fast, ephemeral certificate strictly to satisfy the SslServerAuthenticationOptions.
    /// </summary>
    private static X509Certificate2 GenerateTransientCert()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=MaldactTest", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var transient = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
        return new X509Certificate2(transient.Export(X509ContentType.Pfx));
    }

    /// <summary>
    /// Localized factory stub bypassing ML instantiations.
    /// </summary>
    private class StubInferenceFactory : IInferenceEngineFactory
    {
        public IInferenceEngine Create(StreamTime sessionStartTime) => throw new NotImplementedException();
    }

    /// <summary>
    /// Localized repository stub to satisfy slot dependencies.
    /// </summary>
    private class StubResultRepository : IResultRepository
    {
        public Task SaveAsync(ResultEntry[] results) => Task.CompletedTask;
        public Task<ResultEntry[]> GetAsync(string[] resultIds) => throw new NotImplementedException();
        public Task<ResultEntry?> GetLatestAsync() => throw new NotImplementedException();
        public Task DeleteAsync(string[] resultIds) => throw new NotImplementedException();
        public Task<IEnumerable<ResultEntry>> QueryAsync(ResultQuery query) => throw new NotImplementedException();
        public Task QueryDeleteAsync(ResultQuery query) => throw new NotImplementedException();
        public Task ClearAsync() => throw new NotImplementedException();
        public Task<int> GetCountAsync() => throw new NotImplementedException();
    }
}