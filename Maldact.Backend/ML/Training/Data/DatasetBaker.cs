using System.Text.Json;
using Maldact.Backend.Preprocessing.Pipelines;
using Maldact.Core.Config.ConfigDefinitions;

namespace Maldact.Backend.ML.Training.Data;

/// <summary>
/// Orchestrates the optimized parallel processing, validation, and AOT-safe serialization of machine learning dataset targets.
/// </summary>
public sealed class DatasetBaker
{
    public const string DefaultManifestName = "dataset.json";
    public const string BakedManifestName = "dataset.processed.json";
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Discovers the manifest inside the source directory, bakes all streams in parallel using bounded runtime worker pools, 
    /// and performs short-circuit fault management.
    /// </summary>
    /// <param name="sourceDirectory">The directory path containing the raw dataset manifest and its associated binary streams.</param>
    /// <param name="targetDirectory">The destination directory where the processed tensors and stamped manifest will be written.</param>
    /// <param name="contract">The preprocessing contract containing the transformations to apply to the streams.</param>
    /// <param name="maxDegreeOfParallelism">The maximum number of concurrent data streams to process simultaneously.</param>
    /// <exception cref="ArgumentException">Thrown when input paths are invalid or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the core manifest or targeted binary files are missing.</exception>
    /// <exception cref="InvalidOperationException">Thrown when structural configuration preconditions are unfulfilled.</exception>
    /// <exception cref="InvalidDataException">Thrown when structural validation steps detect corrupt or unaligned inputs.</exception>
    public async Task ProcessDatasetAsync(
        string sourceDirectory, 
        string targetDirectory, 
        PreprocessingContract contract, 
        int maxDegreeOfParallelism)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (string.IsNullOrWhiteSpace(sourceDirectory)) throw new ArgumentException("Source directory path is required.", nameof(sourceDirectory));
        if (string.IsNullOrWhiteSpace(targetDirectory)) throw new ArgumentException("Target directory path is required.", nameof(targetDirectory));
        if (contract.InputDimension == null) throw new InvalidOperationException("The configuration contract must specify an InputDimension.");

        string manifestSourcePath = Path.Combine(sourceDirectory, DefaultManifestName);
        if (!File.Exists(manifestSourcePath))
        {
            throw new FileNotFoundException($"Failed to locate the base dataset manifest layout: {manifestSourcePath}");
        }

        // utilize source generated deserialization to eliminate reflection startup overhead
        string rawJson = await File.ReadAllTextAsync(manifestSourcePath);
        var manifest = JsonSerializer.Deserialize(rawJson, DatasetJsonContext.Default.DatasetManifest) 
            ?? throw new InvalidDataException($"Dataset manifest at '{manifestSourcePath}' resolved to an invalid null state.");
        
        manifest.Validate(sourceDirectory);

        Directory.CreateDirectory(targetDirectory);

        var allStreams = new List<StreamManifest>();
        allStreams.AddRange(manifest.TrainingStreams);
        allStreams.AddRange(manifest.CrossValidationStreams);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            CancellationToken = CancellationToken.None // bound context can pass structural signals if needed
        };

        var baker = new StreamBaker(() => (contract.BuildPipeline(), (float)manifest.GlobalSampleRateHz));

        // execute bounded parallel processing without premature task heap flooding
        await Parallel.ForEachAsync(allStreams, parallelOptions, async (stream, cancellationToken) =>
        {
            string rawPath = Path.Combine(sourceDirectory, stream.FileName);
            string bakedPath = Path.Combine(targetDirectory, stream.FileName);

            string? subDir = Path.GetDirectoryName(bakedPath);
            if (subDir != null)
            {
                Directory.CreateDirectory(subDir);
            }

            await baker.BakeStreamAsync(rawPath, bakedPath, contract.InputDimension.Value);
        });

        var stampedManifest = manifest with { Preprocessing = contract };
        string manifestTargetPath = Path.Combine(targetDirectory, BakedManifestName);

        // serialize using static pre-compiled context structures
        string serializedManifest = JsonSerializer.Serialize(stampedManifest, DatasetJsonContext.Default.DatasetManifest);
        await File.WriteAllTextAsync(manifestTargetPath, serializedManifest);
    }
}