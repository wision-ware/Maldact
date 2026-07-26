using Maldact.Core.Config.ConfigDefinitions;

namespace Maldact.Common.Configuration.Extensions;

/// <summary>
/// Provides extension methods for resolving final sample rates after preprocessing pipeline transformations.
/// </summary>
public static class PreprocessingContractSampleRateGetterExtensions
{
    /// <summary>
    /// Resolves the final output sample rate by identifying the terminal resampling step in the pipeline.
    /// </summary>
    /// <param name="preprocessingContract">The preprocessing contract containing the pipeline.</param>
    /// <returns>The final resolved sample rate in Hz.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the input sample rate is undefined.</exception>
    public static double GetSampleRate(this PreprocessingContract preprocessingContract)
    {
        ArgumentNullException.ThrowIfNull(preprocessingContract);

        if (preprocessingContract.InputSampleRate == null)
            throw new InvalidOperationException("PreprocessingContract is missing the required InputSampleRate.");

        // fetch the last resampling step definition found in the pipeline
        var lastResampling = preprocessingContract.Pipeline
            .LastOrDefault(s => s.Type == PreprocessingStep.PreprocessingStepType.Resampling && s.Resampling?.TargetHz != null);

        return lastResampling?.Resampling?.TargetHz ?? preprocessingContract.InputSampleRate.Value;
    }
}