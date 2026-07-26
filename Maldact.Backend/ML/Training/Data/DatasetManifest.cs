using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.ML;
using Maldact.Core.ML.Training;

namespace Maldact.Backend.ML.Training.Data;

/// <summary>
/// Provides high-performance, compile-time generated JSON serialization metadata for dataset configurations.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(DatasetManifest))]
[JsonSerializable(typeof(StreamManifest))]
[JsonSerializable(typeof(EventManifest))]
internal partial class DatasetJsonContext : JsonSerializerContext;

/// <summary>
/// Represents a serialized ground truth event within a specific data stream.
/// </summary>
public record EventManifest
{
    /// <summary>
    /// The string label of the classification class.
    /// </summary>
    [JsonPropertyName("class")]
    public required string ClassLabel { get; init; }
    
    /// <summary>
    /// The starting timestamp of the event in milliseconds.
    /// </summary>
    public required double StartMs { get; init; }
    
    /// <summary>
    /// The ending timestamp of the event in milliseconds.
    /// </summary>
    public required double EndMs { get; init; }

    /// <summary>
    /// Validates the temporal logic of the event boundaries.
    /// </summary>
    public void Validate()
    {
        if (StartMs >= EndMs) 
            throw new InvalidDataException(
                $"Event '{ClassLabel}' has invalid timestamps: Start ({StartMs}) >= End ({EndMs}).");
    }
    
    /// <summary>
    /// Converts this serialized manifest object into an active ML domain event using an O(1) class dictionary.
    /// </summary>
    public AbsoluteEvent ToAbsoluteEvent(IReadOnlyDictionary<string, ClassificationClass> classLookup)
    {
        if (!classLookup.TryGetValue(ClassLabel, out var mappedClass))
        {
            throw new InvalidDataException(
                $"Manifest contains event for '{ClassLabel}', but this class is not defined in your model specification.");
        }

        return new AbsoluteEvent
        {
            Class = mappedClass,
            StartOffset = TimeSpan.FromMilliseconds(StartMs),
            EndOffset = TimeSpan.FromMilliseconds(EndMs)
        };
    }
}

/// <summary>
/// Represents a single continuous recording file and its associated ground truth events.
/// </summary>
public record StreamManifest
{
    /// <summary>
    /// The local filename of the binary data stream.
    /// </summary>
    public required string FileName { get; init; }
    
    /// <summary>
    /// The total temporal duration of the recording in milliseconds.
    /// </summary>
    public required double DurationMs { get; init; }
    
    /// <summary>
    /// The list of labeled events that occur within this stream.
    /// </summary>
    public List<EventManifest> Events { get; init; } = new();

    /// <summary>
    /// Validates that the referenced file exists and that all events fit within the stream's duration.
    /// </summary>
    public void Validate(string datasetDirectory)
    {
        string fullPath = Path.Combine(datasetDirectory, FileName);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException(
                $"Manifest references missing binary file: {fullPath}");

        foreach (var ev in Events)
        {
            ev.Validate();
            if (ev.EndMs > DurationMs)
                throw new InvalidDataException(
                    $"Event '{ev.ClassLabel}' ends after the stream duration in {FileName}.");
        }
    }
}

/// <summary>
/// The root configuration file defining an entire machine learning dataset, its files, and its preprocessing requirements.
/// </summary>
public record DatasetManifest
{
    /// <summary>
    /// The display name of the dataset.
    /// </summary>
    public required string DatasetName { get; init; }
    
    /// <summary>
    /// The base sampling frequency of the raw binary streams.
    /// </summary>
    public required double GlobalSampleRateHz { get; init; }
    
    /// <summary>
    /// The streams dedicated to training the model.
    /// </summary>
    public List<StreamManifest> TrainingStreams { get; init; } = [];
    
    /// <summary>
    /// The streams dedicated to evaluating model generalization.
    /// </summary>
    public List<StreamManifest> CrossValidationStreams { get; init; } = [];
    
    /// <summary>
    /// The complete set of classes the model will be trained to recognize.
    /// </summary>
    public required string[] Classes { get; init; }
    
    /// <summary>
    /// The data signal processing pipeline applied before tensor formatting.
    /// </summary>
    public PreprocessingContract? Preprocessing { get; init; }
    
    /// <summary>
    /// Indicates whether the dataset has a valid preprocessing pipeline configured.
    /// </summary>
    public bool TrainingReady => Preprocessing is not null;
    
    /// <summary>
    /// Fully validates the dataset's file paths, temporal boundaries, and preprocessing contract.
    /// </summary>
    public void Validate(string datasetDirectory)
    {
        if (GlobalSampleRateHz <= 0) 
            throw new InvalidDataException("GlobalSampleRateHz must be greater than zero.");
            
        if (TrainingStreams.Count == 0)
            throw new InvalidDataException("Dataset contains no training streams.");
        
        if (CrossValidationStreams.Count == 0)
            throw new InvalidDataException("Dataset contains no cross-validation streams.");

        foreach (var stream in TrainingStreams)
        {
            stream.Validate(datasetDirectory);
        }
        
        foreach (var stream in CrossValidationStreams)
        {
            stream.Validate(datasetDirectory);
        }
        
        // validate contract if present (stream ready manifest)
        if (Preprocessing is not null)
        {
            var validationContext = new ValidationContext(Preprocessing);
            var validationResults = new List<ValidationResult>();

            bool isValid = Validator.TryValidateObject(Preprocessing, validationContext, validationResults, validateAllProperties: true);

            if (!isValid)
            {
                var errors = string.Join("\n", validationResults.Select(r => $"- {r.ErrorMessage}"));
                throw new InvalidDataException($"Dataset preprocessing contract is invalid:\n{errors}");
            }
        }
    }
}