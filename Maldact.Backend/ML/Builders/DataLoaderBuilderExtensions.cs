using Maldact.Backend.ML.Training.BatchFormatting;
using Maldact.Backend.ML.Training.Data;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.Data;
using Maldact.Core.ML.Training;

namespace Maldact.Backend.ML.Builders;

/// <summary>
/// Provides extension methods to securely wrap raw streaming data loaders with algorithm-specific formatting pipelines.
/// </summary>
public static class DataLoaderBuilderExtensions
{
    /// <summary>
    /// Injects an algorithm-specific batch formatter into the raw data stream, validating bounds and calculating temporal windows.
    /// </summary>
    /// <param name="rawDatasetLoader">The underlying unformatted streaming dataset.</param>
    /// <param name="modelSpecification">The architectural boundaries dictating window and stride shapes.</param>
    /// <param name="trainingConfiguration">The configuration dictating batch sizes and epoch constraints.</param>
    /// <param name="seed">The optional seed to ensure deterministic behavior.</param>
    /// <returns>An orchestrated data loader ready for continuous stream processing.</returns>
    /// <exception cref="ArgumentNullException">Thrown if any required dependency or configuration payload is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if critical dimension, batch bounds, or sample rates are missing or invalid.</exception>
    /// <exception cref="NotSupportedException">Thrown if the algorithm type lacks a corresponding batch formatter.</exception>
    public static IFormattedLabeledTrainingDataLoader AddFormatting(
        this IRawLabeledTrainingDataLoader rawDatasetLoader,
        ModelSpecification modelSpecification, 
        TrainingConfiguration trainingConfiguration, 
        int? seed = null)
    {
        if (rawDatasetLoader == null) throw new ArgumentNullException(nameof(rawDatasetLoader));
        if (modelSpecification == null) throw new ArgumentNullException(nameof(modelSpecification));
        if (trainingConfiguration == null) throw new ArgumentNullException(nameof(trainingConfiguration));

        if (rawDatasetLoader.SampleRateHz <= 0) 
            throw new InvalidOperationException("Raw dataset loader must provide a strictly positive sample rate.");

        var windowSize = modelSpecification.WindowSize 
            ?? throw new InvalidOperationException("Model specification must define a positive WindowSize.");
            
        var windowStride = modelSpecification.WindowStride 
            ?? throw new InvalidOperationException("Model specification must define a positive WindowStride.");
            
        var batchSize = trainingConfiguration.BatchSize 
            ?? throw new InvalidOperationException("Training configuration must define a strictly positive BatchSize.");

        // frames / frequency = duration
        TimeSpan windowLength = TimeSpan.FromSeconds(windowSize / rawDatasetLoader.SampleRateHz);
        TimeSpan strideLength = TimeSpan.FromSeconds(windowStride / rawDatasetLoader.SampleRateHz);
        
        string[] classNames = rawDatasetLoader.Classes.Select(c => c.ClassName).ToArray();

        ILabeledBatchFormatter formatter = modelSpecification.Algorithm switch
        {
            ModelSpecification.AlgorithmType.Cnn => new CnnLabeledBatchFormatter(classNames, windowLength),
            ModelSpecification.AlgorithmType.Gru => new GruLabeledBatchFormatter(classNames, windowLength),
            ModelSpecification.AlgorithmType.TreeEnsemble => new TreeLabeledBatchFormatter(classNames, windowLength),
            _ => throw new NotSupportedException($"Algorithm {modelSpecification.Algorithm} does not have a supported batch formatter.")
        };

        return new StreamingDataLoader(
            rawDatasetLoader,
            formatter,
            batchSize,
            trainingConfiguration.BatchesPerEpoch ?? 0, 
            strideLength,
            windowLength,
            seed: seed
        );
    }
}