using System.Net;
using System.Net.Sockets;
using System.Xml;
using Maldact.Backend.Diagnostics;
using Maldact.Backend.ML;
using Maldact.Backend.ML.Builders;
using Maldact.Backend.ML.Inference;
using Maldact.Backend.ML.Packaging;
using Maldact.Backend.Server;
using Maldact.Backend.Server.Authentication;
using Maldact.Backend.Server.Builders;
using Maldact.Backend.Server.Results;
using Maldact.Backend.Server.Streaming;
using Maldact.CLI.Common;
using Maldact.Client.Indexing;
using Maldact.Common.Configuration.JsonConfiguration;
using Maldact.Core.Config;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.Server;
using Spectre.Console;
using TorchSharp;

namespace Maldact.CLI.Commands.Server;

using Spectre.Console.Cli;
using System.ComponentModel;

/// <summary>
/// Defines the command-line arguments and configuration paths required to initialize the server runtime securely.
/// </summary>
public sealed class ServerStartSettings : GlobalCommandSettings
{
    /// <summary>Initializes the port bound to the persistent server control channel.</summary>
    [CommandArgument(0, "[controlPort]")]
    [Description("Port to bind the server control to")]
    public int ControlPort { get; init; }
    
    /// <summary>Initializes the port allocated for incoming telemetry data streams.</summary>
    [CommandArgument(1, "[streamingPort]")]
    [Description("Port for data streaming")]
    public int StreamingPort { get; init; }

    /// <summary>Initializes the absolute filesystem path pointing to the deployment artifact zip file.</summary>
    [CommandArgument(2, "[deploymentArtifactZip]")]
    [Description("Path to deployment artifact zip file")]
    public string ArtifactPath { get; init; } = string.Empty;
    
    /// <summary>Initializes the optional path to an overriding server configuration file.</summary>
    [CommandOption("--config <serverConfig>")]
    [Description("Path to server config file")]
    public string? ConfigPath { get; init; }
}

/// <summary>
/// Orchestrates server initialization steps, visual bootstrapping, and diagnostic dashboard rendering.
/// </summary>
public sealed class ServerStartCommand : AsyncCommand<ServerStartSettings>
{
    internal static DeploymentArtifact? ArtifactOverride = null;
    
    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, ServerStartSettings settings, CancellationToken ct)
    {
        IConfigurationProvider<ServerConfiguration> serverConfigProvider;
        ServerRuntime? runtime = null;
        int ctPort = settings.ControlPort;
        
        await Terminal.Error.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("yellow bold"))
            .StartAsync("Starting Maldact server...", async ctx =>
            {
                ctx.Status("Loading server configuration...");
                if (settings.ConfigPath is null)
                {
                    serverConfigProvider = GlobalConfigurationIndexManager.GetServerConfigurationProvider() 
                                           ?? throw new InvalidDataException("No server configuration found in application index.");
                }
                else
                {
                    serverConfigProvider = new JsonConfigurationProvider<ServerConfiguration>(settings.ConfigPath);
                }

                ctx.Status("Loading SSL certificate...");
                var certificate = CertificateManager.GetCertificate();

                ctx.Status($"Loading deployment artifact zip: {Path.GetFileName(settings.ArtifactPath)}...");
                var artifact = ArtifactOverride ?? await ArtifactManager.LoadDeploymentZipAsync(settings.ArtifactPath);

                ctx.Status("Initializing inference engine...");
                var inferenceFactory = new InferenceEngineFactory(artifact);
                var authenticator = serverConfigProvider.Config.BuildAuthenticator();

                ctx.Status("Binding TCP listeners...");
                var serverListener = new TcpListener(IPAddress.Loopback, settings.ControlPort);
                var streamingListener = new TcpListener(IPAddress.Loopback, settings.StreamingPort);
                
                ctPort = ((IPEndPoint)serverListener.LocalEndpoint).Port;
                
                runtime = new ServerRuntime(
                    controlTcpListener: serverListener,
                    streamingTcpListener: streamingListener,
                    certificate: certificate,
                    authenticator: authenticator,
                    inferenceEngineFactory: inferenceFactory,
                    resultRepositoryFactory: () => new InMemoryResultRepository()
                );
                
                await Task.Delay(200); // clear visual transition pacing
            });
        
        using var serverCancellation = new CancellationTokenSource();
        var currentMetrics = new ServerDiagnosticMetrics(DateTimeOffset.Now, 0, 0, 0, 0, Array.Empty<StreamDiagnostic>());
        var progress = new Progress<ServerDiagnosticMetrics>(m => currentMetrics = m);

        var runtimeTask = runtime!.RunAsync(serverCancellation.Token, progress);

        ConsoleCancelEventHandler cancelHandler = (s, e) => e.Cancel = true;
        Console.CancelKeyPress += cancelHandler;

        try
        {
            bool shutdownConfirmed = false;
            while (!shutdownConfirmed)
            {
                Terminal.Out.MarkupLine($"[bold green]✔ Server running on port[/] [yellow]{ctPort}[/][bold green]." +
                                        $"[/] Press [yellow]'Q'[/] to stop.\n");

                await Terminal.Out.Live(currentMetrics.RenderDashboard())
                    .StartAsync(async ctx =>
                    {
                        while (!ct.IsCancellationRequested && !runtime.IsRemotelyKilled)
                        {
                            ctx.UpdateTarget(currentMetrics.RenderDashboard());

                            if (!Console.IsInputRedirected && Console.KeyAvailable)
                            {
                                var keyInfo = Console.ReadKey(intercept: true);
                                if (keyInfo.Key == ConsoleKey.Q || 
                                   keyInfo is { Modifiers: ConsoleModifiers.Control, Key: ConsoleKey.C })
                                {
                                    break;
                                }
                            }

                            await Task.Delay(250);
                        }
                    });

                Terminal.Error.MarkupLine("\n[bold red]Shutdown requested.[/]");
                
                shutdownConfirmed = true; 
                
                if (runtime.IsRemotelyKilled)
                {
                    Terminal.Error.MarkupLine("\n[bold red]Remote shutdown command received.[/]");
                    shutdownConfirmed = true; 
                }
                
                else if (!Console.IsInputRedirected)
                {
                    shutdownConfirmed = await Terminal.Error.ConfirmAsync(
                        "Are you sure you want to stop the server? Unsaved cached data will be lost.", 
                        defaultValue: false);
                }

                if (!shutdownConfirmed)
                {
                    Terminal.Error.MarkupLine("[grey]Resuming dashboard...[/]");
                    await Task.Delay(800);
                }
            }

            Terminal.Error.MarkupLine("[grey]Stopping background services...[/]");
            await serverCancellation.CancelAsync();

            try
            {
                await runtimeTask;
            }
            catch (OperationCanceledException)
            {
                // expected future teardown sequence
            }

            Terminal.Out.MarkupLine("[bold green]Server stopped safely.[/]");
            return 0;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }
}