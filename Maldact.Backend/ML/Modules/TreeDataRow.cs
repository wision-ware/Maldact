using Microsoft.ML.Data;

namespace Maldact.Backend.ML.Modules;

/// <summary>
/// Represents a flattened tensor row formatted for ML.NET classical training algorithms.
/// </summary>
public class TreeDataRow
{
    /// <summary>
    /// Flattened feature vector
    /// </summary>
    [VectorType]
    public float[] Features { get; set; } = [];

    /// <summary>
    /// Single label index representing the target class
    /// </summary>
    public uint Label { get; set; } = 0;
}