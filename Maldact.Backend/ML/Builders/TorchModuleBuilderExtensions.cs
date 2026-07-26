using Maldact.Backend.ML.Modules;
using Maldact.Core.Config.ConfigDefinitions;

namespace Maldact.Backend.ML.Builders;

/// <summary>
/// Provides extension methods for securely instantiating TorchSharp neural network modules from configuration DTOs.
/// </summary>
public static class TorchModuleBuilderExtensions
{
    /// <summary>
    /// Constructs a Convolutional Neural Network (CNN) module for time-series evaluation.
    /// </summary>
    /// <param name="cnnParams">The parameters for the CNN module.</param>
    /// <param name="name">The name of the module as required by the torch base class.</param>
    /// <param name="inputDimension">The number of features on the input.</param>
    /// <param name="outputDimension">The number of features on the output.</param>
    /// <returns>Returns the fully initialized CNN module based on the parameters.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the configuration is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if dimensional bounds are zero or negative.</exception>
    /// <exception cref="ArgumentException">Thrown if the channel sizes aren't properly defined.</exception>
    public static TimeSeriesCnn BuildModule(
        this CnnParameters cnnParams, 
        string name, 
        int inputDimension, 
        int outputDimension)
    {
        if (cnnParams == null) throw new ArgumentNullException(nameof(cnnParams));
        if (inputDimension <= 0) throw new ArgumentOutOfRangeException(nameof(inputDimension), "Input dimension must be positive.");
        if (outputDimension <= 0) throw new ArgumentOutOfRangeException(nameof(outputDimension), "Output dimension must be positive.");

        var channels = cnnParams.ChannelSizes 
            ?? throw new ArgumentException("CNN channel sizes must be explicitly defined.", nameof(cnnParams));

        return new TimeSeriesCnn(
            name ?? "TimeSeriesCnn",
            inputDimension,
            channels,
            cnnParams.KernelSize ?? 3,
            outputDimension
        );
    }

    /// <summary>
    /// Constructs a Gated Recurrent Unit (GRU) module for time-series sequence evaluation.
    /// </summary>
    /// <param name="gruParams">The parameters for the GRU module.</param>
    /// <param name="name">The name of the module as required by the torch base class.</param>
    /// <param name="inputDimension">The number of features on the input.</param>
    /// <param name="outputDimension">The number of features on the output.</param>
    /// <returns>Returns the fully initialized GRU module based on the parameters.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the configuration is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if dimensional bounds are zero or negative.</exception>
    public static TimeSeriesGru BuildModule(
        this GruParameters gruParams,
        string name,
        int inputDimension,
        int outputDimension)
    {
        if (gruParams == null) throw new ArgumentNullException(nameof(gruParams));
        if (inputDimension <= 0) throw new ArgumentOutOfRangeException(nameof(inputDimension), "Input dimension must be positive.");
        if (outputDimension <= 0) throw new ArgumentOutOfRangeException(nameof(outputDimension), "Output dimension must be positive.");

        return new TimeSeriesGru(
            name ?? "TimeSeriesGru",
            inputDimension,
            gruParams.HiddenSize ?? 64,
            gruParams.NumLayers,
            outputDimension,
            gruParams.Dropout
        );
    }
}