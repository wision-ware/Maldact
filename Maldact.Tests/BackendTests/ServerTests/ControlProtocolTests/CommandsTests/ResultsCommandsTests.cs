using System.CommandLine;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Maldact.Backend.Diagnostics;
using Maldact.Backend.Server;
using Maldact.Backend.Server.ControlProtocol.Commands;
using Maldact.Backend.Server.ControlProtocol.Responses;
using Maldact.Backend.Server.ControlProtocol.Utils;
using Maldact.Backend.Server.Streaming;
using Maldact.Core.ML.Inference;
using Maldact.Core.Results;
using Maldact.Core.Server.Authentication;

namespace Maldact.Tests.BackendTests.ServerTests.ControlProtocolTests.CommandsTests;

/// <summary>
/// Verifies the command line argument parsing, routing security, and repository interactions for result queries.
/// </summary>
public class ResultsCommandsTests
{
    /// <summary>
    /// Proves that unauthenticated execution attempts are safely intercepted by the wrapper.
    /// </summary>
    [Fact]
    public async Task Execute_Unauthenticated_ReturnsUnauthorized()
    {
        // arrange
        var context = CreateContext(isAuthenticated: false, hasSlot: false, out var repo);
        var (config, errCapture) = BuildCommandConfig(context);

        // act
        var exitCode = await config.InvokeAsync("latest");

        // assert
        exitCode.Should().Be(0, "System.CommandLine discards handler return integers; 0 proves the app caught the error and didn't crash.");
        errCapture.GetCommandResponse().Message.Should().Contain("Unauthorized");
        repo.GetLatestCalled.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that commands gracefully output an error message when the session 
    /// is authenticated but lacks an active repository slot.
    /// </summary>
    [Fact]
    public async Task Execute_NoSlot_ReturnsRepositoryNotFound()
    {
        // arrange
        var context = CreateContext(isAuthenticated: true, hasSlot: false, out var repo);
        var (config, errCapture) = BuildCommandConfig(context);

        // act
        var exitCode = await config.InvokeAsync("query");

        // assert
        exitCode.Should().Be(0, "System.CommandLine discards handler return integers; 0 proves the app caught the error and didn't crash.");
        errCapture.GetCommandResponse().Message.Should().Contain("No existing result repository found");
        repo.QueryCalled.Should().BeFalse();
    }

    /// <summary>
    /// Proves that providing valid result IDs correctly bridges the command line arguments to the repository get method.
    /// </summary>
    [Fact]
    public async Task Get_WithValidIds_CallsRepositoryGetAsync()
    {
        // arrange
        var context = CreateContext(isAuthenticated: true, hasSlot: true, out var repo);
        var (config, _) = BuildCommandConfig(context);

        // act
        var exitCode = await config.InvokeAsync("get req-1 req-2");

        // assert
        exitCode.Should().Be(0);
        repo.GetIdsCalled.Should().BeTrue();
        repo.LastIdsRequested.Should().BeEquivalentTo("req-1", "req-2");
    }

    /// <summary>
    /// Verifies that the nested subcommand structure and complex temporal/class filtering options 
    /// perfectly parse into a strongly-typed ResultQuery without crashing.
    /// </summary>
    [Fact]
    public async Task QueryDelete_WithFilters_BuildsCorrectResultQuery()
    {
        // arrange
        var context = CreateContext(isAuthenticated: true, hasSlot: true, out var repo);
        var (config, _) = BuildCommandConfig(context);

        // act: notice 'delete' is successfully traversed as a subcommand of 'query'
        var exitCode = await config.InvokeAsync("query delete --min 1000 --max 5000 --filter car truck");

        // assert
        exitCode.Should().Be(0, "Parsing succeeded without string binding crashes.");
        repo.QueryDeleteCalled.Should().BeTrue("the query delete handler must have been routed to.");
        
        var query = repo.LastQuery;
        query.Should().NotBeNull();
        
        // verify StreamTime parsing boundaries
        query!.MinTime.Should().NotBeNull();
        query.MinTime!.Value.IsAbsolute.Should().BeFalse(); // parsed from double '1000' -> ms
        
        query.MaxTime.Should().NotBeNull();
        query.MaxTime!.Value.IsAbsolute.Should().BeFalse(); // parsed from double '5000' -> ms

        // verify Class mapping
        query.Classes.Should().NotBeNull();
        query.Classes.Should().HaveCount(2);
        query.Classes!.Select(c => c.ClassName).Should().BeEquivalentTo("car", "truck");
    }

    // --- SETUP HELPERS & STUBS ---

    /// <summary>
    /// Wraps the command tree in an isolated invocation configuration with capture streams.
    /// </summary>
    private static (CommandLineConfiguration Config, CommandResultCapture ErrorCapture) BuildCommandConfig(SessionContext context)
    {
        var command = new ResultsCommands(context).Command;
        var config = new CommandLineConfiguration(command);
        
        var errCapture = new CommandResultCapture { ResultType = CommandResponse.Type.Error };
        config.Output = new CommandResultCapture(); // suppress standard output in tests
        config.Error = errCapture;

        return (config, errCapture);
    }

    /// <summary>
    /// Bootstraps a secure test context capable of satisfying or rejecting authentication and slot requirements.
    /// </summary>
    private static SessionContext CreateContext(bool isAuthenticated, bool hasSlot, out SpyResultRepository repo)
    {
        repo = new SpyResultRepository();
        var capturedRepo = repo;

        SessionContext.SlotTryGetter getter = (AccessPass p, out StreamingSlot? s) => 
        { 
            if (!hasSlot) { s = null; return false; }
            s = new StreamingSlot(capturedRepo);
            s.TryActivate("127.0.0.1");
            return true;
        };

        SessionContext.SlotTryBooker booker = (AccessPass p, out StreamingSlot? s) => { s = null; return false; };
        Func<ServerDiagnosticMetrics?> diag = () => null;

        var context = new SessionContext(
            new StubAuthenticator(), 
            CreateDummyStreamingManager(), 
            getter, booker, diag, 
            new CancellationTokenSource());

        if (isAuthenticated)
        {
            context.SetAuthenticatedState(new AccessPass(AccessPass.Role.User));
        }

        return context;
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

    private class StubAuthenticator : IAuthenticator { public bool Authenticate(AuthToken token, out AccessPass? pass) { pass = null; return false; } }
    private class StubInferenceFactory : IInferenceEngineFactory { public IInferenceEngine Create(StreamTime sessionStartTime) => throw new NotImplementedException(); }

    /// <summary>
    /// A localized spy to record exact repository interactions and verify argument mapping.
    /// </summary>
    private class SpyResultRepository : IResultRepository
    {
        public bool GetLatestCalled { get; private set; }
        public bool QueryCalled { get; private set; }
        public bool QueryDeleteCalled { get; private set; }
        public bool GetIdsCalled { get; private set; }
        
        public ResultQuery? LastQuery { get; private set; }
        public string[]? LastIdsRequested { get; private set; }

        public Task<ResultEntry?> GetLatestAsync()
        {
            GetLatestCalled = true;
            return Task.FromResult<ResultEntry?>(null);
        }

        public Task<IEnumerable<ResultEntry>> QueryAsync(ResultQuery query)
        {
            QueryCalled = true;
            LastQuery = query;
            return Task.FromResult<IEnumerable<ResultEntry>>(Array.Empty<ResultEntry>());
        }

        public Task QueryDeleteAsync(ResultQuery query)
        {
            QueryDeleteCalled = true;
            LastQuery = query;
            return Task.CompletedTask;
        }

        public Task<ResultEntry[]> GetAsync(string[] resultIds)
        {
            GetIdsCalled = true;
            LastIdsRequested = resultIds;
            return Task.FromResult(Array.Empty<ResultEntry>());
        }

        public Task DeleteAsync(string[] resultIds) => Task.CompletedTask;
        public Task SaveAsync(ResultEntry[] results) => Task.CompletedTask;
        public Task ClearAsync() => Task.CompletedTask;
        public Task<int> GetCountAsync() => Task.FromResult(0);
    }
}