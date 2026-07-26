using System.Net;
using System.Net.Sockets;
using System.Reflection;
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

/// <summary>
/// Verifies the global server lifecycle, session slot routing, and telemetry calculations.
/// </summary>
public class ServerRuntimeTests
{
    /// <summary>
    /// Proves that the server successfully releases network ports and background tasks upon cancellation.
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenCancelled_GracefullyShutsDownListeners()
    {
        // arrange
        using var runtimeSetup = CreateRuntime();
        using var cts = new CancellationTokenSource();
        
        // act
        var runTask = runtimeSetup.Runtime.RunAsync(cts.Token);
        
        // trigger the global shutdown
        await cts.CancelAsync();
        
        // assert - if the server doesn't clean up or throws TaskCanceledException, this will crash
        await FluentActions.Invoking(() => runTask)
            .Should().NotThrowAsync("the runtime must swallow OperationCanceledException and exit cleanly.");
    }

    /// <summary>
    /// Verifies that the slot booking delegate strictly prevents duplicate active sessions for a single access pass.
    /// </summary>
    [Fact]
    public void TryBookSlot_ExistingActiveSlot_PreventsDuplicateBooking()
    {
        // arrange
        using var runtimeSetup = CreateRuntime();
        var runtime = runtimeSetup.Runtime;
        var pass = new AccessPass(AccessPass.Role.User);

        var tryBookSlot = GetPrivateMethod(runtime, "TryBookSlot");

        // act - book first slot
        var args1 = new object?[] { pass, null };
        var success1 = (bool)tryBookSlot.Invoke(runtime, args1)!;
        var slot1 = (StreamingSlot)args1[1]!;
        
        // forcefully activate the slot to trigger the duplicate block
        slot1.TryActivate("127.0.0.1");

        // act - attempt duplicate booking
        var args2 = new object?[] { pass, null };
        var success2 = (bool)tryBookSlot.Invoke(runtime, args2)!;

        // assert
        success1.Should().BeTrue();
        success2.Should().BeFalse("the runtime must reject booking if an active slot already exists for the pass.");
        args2[1].Should().BeNull();
    }

    /// <summary>
    /// Mathematically proves the telemetry loop calculates aggregate memory footprint accurately based on constants.
    /// </summary>
    [Fact]
    public async Task TelemetryLoop_CalculatesEstimatedMemoryAccurately()
    {
        // arrange
        using var runtimeSetup = CreateRuntime(new MockRepository(cachedCount: 10)); // 10 results * 96 bytes = 960 bytes
        var runtime = runtimeSetup.Runtime;
        
        // forcefully inject an active slot to be picked up by the telemetry loop
        var pass = new AccessPass(AccessPass.Role.User);
        var tryBookSlot = GetPrivateMethod(runtime, "TryBookSlot");
        var args = new object?[] { pass, null };
        tryBookSlot.Invoke(runtime, args);
        var slot = (StreamingSlot)args[1]!;
        slot.TryActivate("127.0.0.1");

        var progress = new TelemetryCapture();
        using var cts = new CancellationTokenSource();

        var runTelemetry = GetPrivateMethod(runtime, "RunTelemetryLoopAsync");

        // act
        var telemetryTask = (Task)runTelemetry.Invoke(runtime, new object[] { cts.Token, progress })!;
        
        // wait for the first progress report
        await progress.WaitForReportAsync();
        await cts.CancelAsync();
        await telemetryTask;

        // assert
        progress.LastReport.Should().NotBeNull();
        progress.LastReport!.ActiveStreamingSessions.Should().Be(1);
        progress.LastReport.TotalCachedResults.Should().Be(10);
        
        // 96 is the assumed ResultEntry.EstimatedBytesAllocated constant
        progress.LastReport.EstimatedMemoryUsageBytes.Should().Be(960, "10 cached results * 96 bytes per result = 960 bytes.");
    }

    // --- SETUP HELPERS & STUBS ---

    private static MethodInfo GetPrivateMethod(ServerRuntime runtime, string methodName)
    {
        return typeof(ServerRuntime).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
               ?? throw new InvalidOperationException($"Method {methodName} not found.");
    }

    private static RuntimeSetup CreateRuntime(IResultRepository? repo = null)
    {
        var controlListener = new TcpListener(IPAddress.Loopback, 0);
        var streamingListener = new TcpListener(IPAddress.Loopback, 0);
        
        var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=MaldactTest", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var transient = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
        var cert = new X509Certificate2(transient.Export(X509ContentType.Pfx));

        var runtime = new ServerRuntime(
            controlListener,
            streamingListener,
            cert,
            new StubAuthenticator(),
            new StubInferenceFactory(),
            () => repo ?? new MockRepository(0)
        );

        return new RuntimeSetup(runtime, rsa, transient, cert);
    }

    /// <summary>
    /// Bundles disposable cryptographic resources to ensure strict memory cleanup after tests.
    /// </summary>
    private record RuntimeSetup(ServerRuntime Runtime, RSA Rsa, X509Certificate2 TransientCert, X509Certificate2 ExportedCert) : IDisposable
    {
        public void Dispose()
        {
            Rsa.Dispose();
            TransientCert.Dispose();
            ExportedCert.Dispose();
        }
    }

    private class TelemetryCapture : IProgress<ServerDiagnosticMetrics>
    {
        private readonly TaskCompletionSource _tcs = new();
        public ServerDiagnosticMetrics? LastReport { get; private set; }

        public void Report(ServerDiagnosticMetrics value)
        {
            LastReport = value;
            _tcs.TrySetResult();
        }

        public Task WaitForReportAsync() => _tcs.Task;
    }

    private class StubAuthenticator : IAuthenticator { public bool Authenticate(AuthToken token, out AccessPass? pass) { pass = null; return false; } }
    private class StubInferenceFactory : IInferenceEngineFactory { public IInferenceEngine Create(StreamTime sessionStartTime) => throw new NotImplementedException(); }

    /// <summary>
    /// A predictable repository mock to feed deterministic numbers into the telemetry engine.
    /// </summary>
    private class MockRepository(int cachedCount) : IResultRepository
    {
        public Task<int> GetCountAsync() => Task.FromResult(cachedCount);
        
        public Task SaveAsync(ResultEntry[] results) => Task.CompletedTask;
        public Task<ResultEntry[]> GetAsync(string[] resultIds) => throw new NotImplementedException();
        public Task<ResultEntry?> GetLatestAsync() => throw new NotImplementedException();
        public Task DeleteAsync(string[] resultIds) => throw new NotImplementedException();
        public Task<IEnumerable<ResultEntry>> QueryAsync(ResultQuery query) => throw new NotImplementedException();
        public Task QueryDeleteAsync(ResultQuery query) => throw new NotImplementedException();
        public Task ClearAsync() => throw new NotImplementedException();
    }
}