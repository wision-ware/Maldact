using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Maldact.Core.Config;

namespace Maldact.Common.Configuration.JsonConfiguration;

/// <summary>
/// Provides a JSON-backed, file-based implementation of the configuration provider with atomic writes and strict validation.
/// </summary>
/// <typeparam name="TConfig">The strongly-typed configuration schema.</typeparam>
public sealed class JsonConfigurationProvider<TConfig> : IConfigurationProvider<TConfig> 
    where TConfig: class, new()
{
    private readonly string _filePath;
    private readonly object _stateLock = new();
    
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }, 
        PropertyNameCaseInsensitive = true
    };
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }, 
        WriteIndented = true
    };

    /// <inheritdoc />
    public TConfig Config { get; private set; }
    
    /// <summary>
    /// Initializes a new instance of the provider and immediately loads and validates the configuration from disk.
    /// </summary>
    /// <param name="filePath">The absolute path to the JSON configuration file.</param>
    /// <exception cref="FileNotFoundException">Thrown if the target configuration file does not exist.</exception>
    /// <exception cref="ArgumentException">Thrown if the file path is null or empty.</exception>
    /// <exception cref="InvalidDataException">Thrown if the target configuration is empty or invalid.</exception>
    public JsonConfigurationProvider(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        _filePath = filePath;
        Config = LoadConfig();
    }

    private TConfig LoadConfig()
    {
        if (!File.Exists(_filePath))
            throw new FileNotFoundException($"CRITICAL: Config file missing at {_filePath}");

        try
        {
            // use shared read access to prevent crashes if the file is locked by an external reader
            using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            
            var configInstance = JsonSerializer.Deserialize<TConfig>(stream, ReadOptions) 
                                 ?? throw new InvalidDataException("Configuration file was empty or contained invalid JSON.");

            ValidateState(configInstance, _filePath);
            return configInstance;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse JSON configuration at {_filePath}.", ex);
        }
    }
    
    /// <inheritdoc />
    public void Update(TConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        // strictly enforce validation before allowing in-memory state mutations
        ValidateState(config, "Update Payload");

        lock (_stateLock)
        {
            Config = config;
        }
    }

    /// <inheritdoc />
    public void Flush()
    {
        lock (_stateLock)
        {
            var tempPath = _filePath + ".tmp";
            
            // write to a temporary file first to prevent corruption from mid-write process crashes
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, Config, WriteOptions);
            }
            
            // perform atomic OS-level replacement
            File.Move(tempPath, _filePath, overwrite: true);
        }
    }

    /// <summary>
    /// Validates the configuration model against its data annotation constraints.
    /// </summary>
    private static void ValidateState(TConfig config, string source)
    {
        var validationContext = new ValidationContext(config);
        var validationResults = new List<ValidationResult>();

        if (!Validator.TryValidateObject(config, validationContext, validationResults, validateAllProperties: true))
        {
            var errors = string.Join(Environment.NewLine, validationResults.Select(r => $"- {r.ErrorMessage}"));
            throw new InvalidOperationException($"Configuration validation failed for [{source}]:\n{errors}");
        }
    }
}