using System.CommandLine;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Maldact.Backend.Diagnostics;
using Maldact.Backend.Server;
using Maldact.Backend.Server.ControlProtocol.Responses;
using Maldact.Backend.Server.ControlProtocol.Utils;
using Maldact.Backend.Server.Streaming;
using Maldact.Core.ML.Inference;
using Maldact.Core.Results;
using Maldact.Core.Server.Authentication;

namespace Maldact.Tests.BackendTests.ServerTests.ControlProtocolTests.CommandsTests;

/// <summary>
/// Centralizes the heavy cryptographic and networking boilerplate required to spin up isolated control protocol test contexts.
/// </summary>
internal static class ControlProtocolTestFactory
{
    public static (CommandLineConfiguration Config, CommandResultCapture Output, CommandResultCapture Error) BuildConfig(Command command)
    {
        var config = new CommandLineConfiguration(command);
        var outCapture = new CommandResultCapture();
        var errCapture = new CommandResultCapture { ResultType = CommandResponse.Type.Error };
        
        config.Output = outCapture;
        config.Error = errCapture;

        return (config, outCapture, errCapture);
    }

    public static SessionContext CreateContext(
        bool isAuthenticated, 
        AccessPass.Role role = AccessPass.Role.User,
        IAuthenticator? authenticator = null,
        SessionContext.SlotTryBooker? booker = null,
        Func<ServerDiagnosticMetrics?>? metricsGetter = null)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=MaldactTest", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var transient = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
        var cert = new X509Certificate2(transient.Export(X509ContentType.Pfx));

        var manager = new StreamingManager(listener, new StubInferenceFactory(), cert);

        SessionContext.SlotTryGetter getter = (AccessPass p, out StreamingSlot? s) => { s = null; return false; };
        
        var context = new SessionContext(
            authenticator ?? new StubAuthenticator(), 
            manager, 
            getter, 
            booker ?? ((AccessPass p, out StreamingSlot? s) => { s = null; return false; }), 
            metricsGetter ?? (() => null), 
            new CancellationTokenSource());

        if (isAuthenticated)
        {
            context.SetAuthenticatedState(new AccessPass(role));
        }

        return context;
    }

    public class StubAuthenticator : IAuthenticator { public bool Authenticate(AuthToken token, out AccessPass? pass) { pass = null; return false; } }
    private class StubInferenceFactory : IInferenceEngineFactory { public IInferenceEngine Create(StreamTime sessionStartTime) => throw new NotImplementedException(); }
}