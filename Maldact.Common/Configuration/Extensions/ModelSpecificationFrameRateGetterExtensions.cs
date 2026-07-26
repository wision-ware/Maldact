using Maldact.Core.Config.ConfigDefinitions;

namespace Maldact.Common.Configuration.Extensions;

/// <summary>
/// Provides extension methods for calculating telemetry frame rates based on model architecture.
/// </summary>
public static class ModelSpecificationFrameRateGetterExtensions
{
    /// <summary>
    /// Calculates the effective frame rate based on the sample rate and model architecture constraints.
    /// </summary>
    /// <param name="modelSpecification">The target model specification.</param>
    /// <param name="sampleRate">The incoming signal sample rate.</param>
    /// <returns>The calculated output frame rate.</returns>
    /// <exception cref="InvalidOperationException">Thrown if metadata is missing or invalid.</exception>
    public static double GetFrameRate(this ModelSpecification modelSpecification, double sampleRate)
    {
        ArgumentNullException.ThrowIfNull(modelSpecification);

        if (modelSpecification.Algorithm == null)
            throw new InvalidOperationException("Algorithm type is required to calculate FrameRate.");

        return modelSpecification.Algorithm switch
        {
            ModelSpecification.AlgorithmType.TreeEnsemble => CalculateTreeEnsembleFrameRate(modelSpecification, sampleRate),
            _ => sampleRate
        };
    }

    private static double CalculateTreeEnsembleFrameRate(ModelSpecification modelSpecification, double sampleRate)
    {
        var stride = modelSpecification.WindowStride;
        if (stride is null or <= 0)
            throw new InvalidOperationException("WindowStride must be defined and greater than zero for TreeEnsemble models.");

        return sampleRate / stride.Value;
    }
}