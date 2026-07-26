namespace Maldact.Core.Config.ConfigDefinitions;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Defines the hyperparameters, hardware targets, and algorithmic boundaries for a training session.
/// </summary>
public record TrainingConfiguration
{
    public enum DeviceType { Cpu, Cuda }

    /// <summary>
    /// Bitwise flags representing the discrete consolidation algorithms to evaluate during the hyperparameter tuning phase.
    /// </summary>
    [Flags]
    public enum ConsolidationAlgorithm
    {
        None = 0,
        BasicAttention = 1 << 0,          // 1
        ExponentialHysteresis = 1 << 1,   // 2
        SlidingWindowHysteresis = 1 << 2, // 4
        
        // Convenience flag for exhaustive searches
        All = BasicAttention | ExponentialHysteresis | SlidingWindowHysteresis 
    }
    
    /// <summary>
    /// Gets the bitwise flag combination of temporal algorithms to optimize.
    /// </summary>
    public ConsolidationAlgorithm ConsolidationAlgorithms { get; init; } = ConsolidationAlgorithm.All;
    
    /// <summary>
    /// The absolute maximum number of tuning cycles to perform during grid search.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxTuningCycles { get; init; } = int.MaxValue;
    
    /// <summary>
    /// The random seed for reproducible training runs. Automatically generated if left null.
    /// </summary>
    public int? RandomSeed { get; init; }
    
    /// <summary>
    /// The hardware accelerator target for training (CPU or CUDA).
    /// </summary>
    public DeviceType? Device { get; init; } = DeviceType.Cpu;
    
    /// <summary>
    /// The maximum number of complete passes through the training dataset.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? MaxEpochs { get; init; }

    /// <summary>
    /// The number of chronological sequences propagated through the network before updating gradients.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? BatchSize { get; init; }
    
    /// <summary>
    /// The number of batches to process before finalizing an epoch.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? BatchesPerEpoch { get; init; }

    /// <summary>
    /// The step size used by the optimizer to converge on the loss minimum.
    /// </summary>
    [Range(0.000001, 1.0, ErrorMessage = "Learning rate must be a small positive decimal.")]
    public double? LearningRate { get; init; } = 0.001;
    
    /// <summary>
    /// The number of epochs with no validation improvement before triggering early stopping.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? Patience { get; init; } = 10;
}