namespace Maldact.Client.Indexing;

/// <summary>
/// Represents an immutable snapshot of file paths pointing to the core application configuration files.
/// </summary>
/// <param name="ServerConfigurationPath">The path to the ServerConfiguration config.</param>
/// <param name="PreprocessingContractPath">The path to the PreprocessingContract config.</param>
/// <param name="TrainingConfigurationPath">The path to the TrainingConfiguration config.</param>
/// <param name="ModelSpecificationPath">The path to the ModelSpecification config.</param>
public sealed record ConfigurationIndex(
    string? ServerConfigurationPath,
    string? PreprocessingContractPath,
    string? TrainingConfigurationPath,
    string? ModelSpecificationPath
);