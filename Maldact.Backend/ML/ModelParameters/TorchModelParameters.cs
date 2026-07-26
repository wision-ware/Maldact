using Maldact.Core.ML.Training;

namespace Maldact.Backend.ML.ModelParameters;

/// <summary>
/// Holds the serialized state dictionary for a trained TorchSharp neural network.
/// </summary>
/// <param name="Payload">The native tensor parameters copied into a zero-copy managed memory buffer.</param>
public sealed record TorchModelParameters(ReadOnlyMemory<byte> Payload) : IModelParameters
{
    /// <inheritdoc />
    public ReadOnlyMemory<byte> ToBytes() => Payload;
}