using System.Diagnostics.CodeAnalysis;
using Maldact.CLI.Commands.Config;
using Maldact.CLI.Commands.Config.Get;
using Maldact.CLI.Commands.Config.Set;
using Maldact.CLI.Commands.Connect;
using Maldact.CLI.Commands.Dataset;
using Maldact.CLI.Commands.Disconnect;
using Maldact.CLI.Commands.Model;
using Maldact.CLI.Commands.Results;
using Maldact.CLI.Commands.Server;
using Maldact.CLI.Commands.Status;
using Maldact.CLI.Commands.Stream;
using Spectre.Console.Cli;

namespace Maldact.CLI;

/// <summary>
/// Main entry point class for the Maldact command-line interface.
/// </summary>
internal sealed class Program
{
    /// <summary>
    /// Executes the core command routing application loop.
    /// </summary>
    /// <param name="args">The command-line arguments provided by the operator.</param>
    /// <returns>The execution exit code (0 for success, non-zero for failure).</returns>
    public static async Task<int> Main(string[] args)
    {
        var app = new CommandApp();

        app.Configure(config =>
        {
            config.SetApplicationName("maldact");

            config.PropagateExceptions();

            config.AddBranch("server", server =>
            {
                server.AddCommand<ServerStartCommand>("start");
                server.AddCommand<ServerStopCommand>("stop");
                server.AddCommand<ServerStatusCommand>("status");
            });

            config.AddBranch("model", model =>
            {
                model.AddCommand<ModelTrainCommand>("train");
            });

            config.AddBranch("results", results =>
            {
                results.AddCommand<ResultsLatestCommand>("latest");
                results.AddCommand<ResultsGetCommand>("get");
                results.AddCommand<ResultsDeleteCommand>("delete");
                results.AddCommand<ResultsQueryCommand>("query");
                results.AddBranch("query", query =>
                {
                    query.AddCommand<ResultsQueryDeleteCommand>("delete");
                });
            });

            config.AddCommand<ConnectCommand>("connect");
            config.AddCommand<DisconnectCommand>("disconnect");
            config.AddCommand<StatusCommand>("status");
            config.AddCommand<StreamCommand>("stream");

            config.AddBranch("config", cfg =>
            {
                cfg.AddBranch("get", get =>
                {
                    get.AddCommand<ConfigGetModelSpecificationCommand>("model-specification");
                    get.AddCommand<ConfigGetPreprocessingContractCommand>("preprocessing-contract");
                    get.AddCommand<ConfigGetTrainingConfigurationCommand>("training-configuration");
                    get.AddCommand<ConfigGetServerConfigurationCommand>("server-configuration");
                });
                cfg.AddBranch("set", set =>
                {
                    set.AddCommand<ConfigSetModelSpecificationCommand>("model-specification");
                    set.AddCommand<ConfigSetPreprocessingContractCommand>("preprocessing-contract");
                    set.AddCommand<ConfigSetTrainingConfigurationCommand>("training-configuration");
                    set.AddCommand<ConfigSetServerConfigurationCommand>("server-configuration");
                });
                cfg.AddCommand<ConfigListCommand>("list");
            });

            config.AddBranch("dataset", dataset =>
            {
                dataset.AddCommand<DatasetProcessCommand>("process");
            });
        });

        return await app.RunAsync(args);
    }
}