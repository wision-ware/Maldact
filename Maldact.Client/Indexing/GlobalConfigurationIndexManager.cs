using System.Text.Json;
using Maldact.Common.Configuration.JsonConfiguration;
using Maldact.Core.Config;
using Maldact.Core.Config.ConfigDefinitions;

namespace Maldact.Client.Indexing;

/// <summary>
/// Safely manages the persistent local storage of the application's configuration index.
/// </summary>
public static class GlobalConfigurationIndexManager
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    
    // evaluates dynamically to support runtime test environment injection
    private static string StateFilePath => Path.Combine(IndexingInvariantManager.GetConfigDirectory(), "configIndex.json");

    /// <summary>
    /// Overwrites the current configuration index state safely. Passing null purges the index from disk.
    /// </summary>
    /// <param name="newState">The new state to serialize.</param>
    public static void SetNewState(ConfigurationIndex? newState)
    {
        if (newState == null)
        {
            Clear();
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(StateFilePath)!);

        // utilizes shared read locks to prevent crashes during concurrent external access
        using var stream = new FileStream(StateFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, newState, Options);
    }

    /// <summary>
    /// Reads the current configuration index from disk with read-sharing allowed.
    /// </summary>
    /// <returns>The deserialized configuration index, or null if it does not exist.</returns>
    public static ConfigurationIndex? GetCurrentState()
    {
        if (!File.Exists(StateFilePath)) return null;

        try
        {
            using var stream = new FileStream(StateFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return JsonSerializer.Deserialize<ConfigurationIndex>(stream, Options);
        }
        catch (JsonException)
        {
            return null; // gracefully handle corrupted files without crashing the client
        }
    }
    
    /// <summary>
    /// Retrieves a configuration provider for the server runtime state.
    /// </summary>
    /// <returns>The configuration provider if it exists.</returns>
    public static IConfigurationProvider<ServerConfiguration>? GetServerConfigurationProvider()
    {
        var state = GetCurrentState();
        return state?.ServerConfigurationPath != null ? new JsonConfigurationProvider<ServerConfiguration>(state.ServerConfigurationPath) : null;
    }

    /// <summary>
    /// Retrieves a configuration provider for the active model specification.
    /// </summary>
    /// <returns>The configuration provider if it exists.</returns>
    public static IConfigurationProvider<ModelSpecification>? GetModelSpecificationProvider()
    {
        var state = GetCurrentState();
        return state?.ModelSpecificationPath != null ? new JsonConfigurationProvider<ModelSpecification>(state.ModelSpecificationPath) : null;
    }

    /// <summary>
    /// Retrieves a configuration provider for the preprocessing data contract.
    /// </summary>
    /// <returns>The configuration provider if it exists.</returns>
    public static IConfigurationProvider<PreprocessingContract>? GetPreprocessingContractProvider()
    {
        var state = GetCurrentState();
        return state?.PreprocessingContractPath != null ? new JsonConfigurationProvider<PreprocessingContract>(state.PreprocessingContractPath) : null;
    }
    
    /// <summary>
    /// Retrieves a configuration provider for the training pipeline parameters.
    /// </summary>
    /// <returns>The configuration provider if it exists.</returns>
    public static IConfigurationProvider<TrainingConfiguration>? GetTrainingConfigurationProvider()
    {
        var state = GetCurrentState();
        return state?.TrainingConfigurationPath != null ? new JsonConfigurationProvider<TrainingConfiguration>(state.TrainingConfigurationPath) : null;
    }

    public static bool Verify<TConfig>(string filePath) where TConfig : class, new()
    {
        try
        {
            _ = new JsonConfigurationProvider<TConfig>(filePath);
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }
    
    /// <summary>
    /// Deletes the configuration index state file from disk.
    /// </summary>
    public static void Clear()
    {
        if (File.Exists(StateFilePath)) File.Delete(StateFilePath);
    }
}