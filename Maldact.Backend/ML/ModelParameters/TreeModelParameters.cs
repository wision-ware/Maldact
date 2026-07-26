using Maldact.Core.ML.Training;

namespace Maldact.Backend.ML.ModelParameters;

/// <summary>
/// Holds the serialized pipeline and ensemble node structures for an ML.NET tree model.
/// </summary>
/// <param name="Payload">The binary representation of the fitted ML.NET model and schema.</param>
public sealed record TreeModelParameters(ReadOnlyMemory<byte> Payload) : IModelParameters
{
    /// <inheritdoc />
    public ReadOnlyMemory<byte> ToBytes() => Payload;
}