using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Text.Json;
using Maldact.Backend.ML.ModelParameters;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.ML.Consolidation;
using Maldact.Core.ML.Consolidation.Tuning;
using Maldact.Core.ML.Training;

namespace Maldact.Backend.ML.Packaging;


/// <summary>
/// Manages the serialization, compression, and extraction of model deployment artifacts.
/// </summary>
public static class ArtifactManager
{
    private const string WeightsFileName = "weights.bin";
    private const string SpecFileName = "model_spec.json";
    private const string ContractFileName = "preprocessing_contract.json";
    private const string ConsolidatorFileName = "consolidator_config.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Serializes and packages a deployment artifact directly into a zip archive via forward-only streams.
    /// </summary>
    /// <param name="artifact">The complete model state to package.</param>
    /// <param name="outputZipPath">The destination file path for the archive.</param>
    /// <returns>The confirmed output path of the generated zip file.</returns>
    public static async Task CreateDeploymentZipAsync(DeploymentArtifact artifact, string outputZipPath)
    {
        ValidateDto(artifact.Specification);
        ValidateDto(artifact.Contract);

        if (File.Exists(outputZipPath)) File.Delete(outputZipPath);

        await using var fileStream = new FileStream(outputZipPath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

        await WriteJsonEntryAsync(archive, SpecFileName, artifact.Specification);
        await WriteJsonEntryAsync(archive, ContractFileName, artifact.Contract);
        await WriteJsonEntryAsync(archive, ConsolidatorFileName, artifact.ConsolidatorConfiguration);

        // bypass compression for high-entropy weight binaries to save cpu overhead
        var weightsEntry = archive.CreateEntry(WeightsFileName, CompressionLevel.NoCompression);
        await using var weightsStream = weightsEntry.Open();
        await weightsStream.WriteAsync(artifact.Parameters.ToBytes());
    }

    /// <summary>
    /// Extracts, deserializes, and validates a deployment artifact directly from a zip archive stream.
    /// </summary>
    /// <param name="zipFilePath">The file path of the archive to load.</param>
    /// <returns>The fully assembled deployment artifact.</returns>
    public static async Task<DeploymentArtifact> LoadDeploymentZipAsync(string zipFilePath)
    {
        if (!File.Exists(zipFilePath))
            throw new FileNotFoundException($"Deployment artifact not found at {zipFilePath}");

        await using var fileStream = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

        var spec = await ReadJsonEntryAsync<ModelSpecification>(archive, SpecFileName);
        ValidateDto(spec);

        var contract = await ReadJsonEntryAsync<PreprocessingContract>(archive, ContractFileName);
        ValidateDto(contract);

        var consolidatorParams = await ReadJsonEntryAsync<ConsolidatorConfiguration>(archive, ConsolidatorFileName);

        var weightsEntry = archive.GetEntry(WeightsFileName) 
                           ?? throw new InvalidDataException($"Missing {WeightsFileName} in artifact.");
        
        // rent memory stream specifically for the binary payload
        await using var weightsStream = weightsEntry.Open();
        using var ms = new MemoryStream((int)weightsEntry.Length);
        await weightsStream.CopyToAsync(ms);
        var rawBytes = ms.ToArray();

        IModelParameters parameters = spec.Algorithm switch
        {
            ModelSpecification.AlgorithmType.Gru or ModelSpecification.AlgorithmType.Cnn 
                => new TorchModelParameters(rawBytes),
            
            ModelSpecification.AlgorithmType.TreeEnsemble 
                => new TreeModelParameters(rawBytes),
            
            _ => throw new NotImplementedException($"Unsupported algorithm type: {spec.Algorithm}")
        };

        return new DeploymentArtifact
        {
            Specification = spec,
            Contract = contract,
            Parameters = parameters,
            ConsolidatorConfiguration = consolidatorParams
        };
    }

    /// <summary>
    /// Streams a serialized JSON representation directly into a zip archive entry.
    /// </summary>
    private static async Task WriteJsonEntryAsync<T>(ZipArchive archive, string entryName, T payload)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, payload, JsonOptions);
    }

    /// <summary>
    /// Deserializes a JSON object directly from a zip archive entry stream, avoiding intermediate string allocations.
    /// </summary>
    private static async Task<T> ReadJsonEntryAsync<T>(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName) 
                    ?? throw new InvalidDataException($"Missing {entryName} in artifact.");
        
        await using var stream = entry.Open();
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions) 
               ?? throw new InvalidDataException($"Failed to deserialize {entryName}.");
    }

    /// <summary>
    /// Enforces systemic validation constraints on extracted configuration objects.
    /// </summary>
    private static void ValidateDto(object dto)
    {
        var context = new ValidationContext(dto, serviceProvider: null, items: null);
        var validationResults = new List<ValidationResult>();
        
        var isValid = Validator.TryValidateObject(dto, context, validationResults, validateAllProperties: true);

        if (isValid) return;
        var errors = string.Join(" | ", validationResults.Select(r => r.ErrorMessage));
        throw new ValidationException($"Artifact Integrity Check Failed for {dto.GetType().Name}: {errors}");
    }
}