namespace Maldact.Core.ML.Consolidation.Tuning;

/// <summary>
/// Defines a consolidator that can expose its active configuration state for serialization or telemetry.
/// </summary>
public interface ITunableResultConsolidator : IResultConsolidator
{
    /// <summary>
    /// Retrieves the DTO representing the current tuning parameters of this consolidator.
    /// </summary>
    /// <returns>The configuration object.</returns>
    ConsolidatorConfiguration GetConsolidatorConfiguration();
}