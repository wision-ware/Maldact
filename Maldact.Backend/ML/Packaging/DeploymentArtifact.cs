using System.Text.Json;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.ML.Consolidation;
using Maldact.Core.ML.Consolidation.Tuning;
using Maldact.Core.ML.Training;

namespace Maldact.Backend.ML.Packaging;

/// <summary>
/// Encapsulates the complete required state for deploying a trained model into the inference engine.
/// </summary>
public record DeploymentArtifact
{
    /// <summary>
    /// Gets the topological configuration and metadata of the underlying model architecture.
    /// </summary>
    public required ModelSpecification Specification { get; init; }

    /// <summary>
    /// Gets the preprocessing pipeline required to process raw data for this model.
    /// </summary>
    public required PreprocessingContract Contract { get; init; }

    /// <summary>
    /// Gets the binary parameter weights representing the learned state.
    /// </summary>
    public required IModelParameters Parameters { get; init; }

    /// <summary>
    /// Gets the configuration detailing how sequence predictions are aggregated into results.
    /// </summary>
    public required ConsolidatorConfiguration ConsolidatorConfiguration { get; init; }
}