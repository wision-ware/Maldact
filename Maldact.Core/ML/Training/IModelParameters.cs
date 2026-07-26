namespace Maldact.Core.ML.Training;

/// <summary>
/// Defines the contract for returning serialized model weights from a training loop.
/// </summary>
public interface IModelParameters
{
    /// <summary>
    /// Retrieves the raw byte sequence of the trained model parameters.
    /// </summary>
    /// <returns>A read-only memory buffer containing the serialized state.</returns>
    public ReadOnlyMemory<byte> ToBytes();
}