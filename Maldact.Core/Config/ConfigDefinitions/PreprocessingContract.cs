namespace Maldact.Core.Config.ConfigDefinitions;

using System.ComponentModel.DataAnnotations;


/// <summary>
/// The root configuration payload defining a complete, sequential Data Signal Processing (DSP) pipeline.
/// </summary>
public record PreprocessingContract
{
    /// <summary>
    /// An ordered sequence of processing steps. Data flows from index 0 to the end of the list.
    /// </summary>
    [Required]
    public List<PreprocessingStep> Pipeline { get; init; } = new();
    
    /// <summary>
    /// The final reshaping enforcement applied after all pipeline steps have completed.
    /// </summary>
    [Required]
    public ReshapingOptions? FinalReshaping { get; init; }
    
    /// <summary>
    /// The number of features (variables) per timestep present in the raw input data.
    /// </summary>
    [Required]
    public int? InputDimension { get; init; }
    
    /// <summary>
    /// The sampling frequency (in Hertz) of the raw input data.
    /// </summary>
    [Required]
    public double? InputSampleRate { get; init; }
}

/// <summary>
/// Defines a single transformational step within the DSP pipeline.
/// </summary>
public record PreprocessingStep : IValidatableObject
{
    /// <summary>
    /// The mathematical category of this processing step.
    /// </summary>
    public enum PreprocessingStepType
    {
        Imputation,     
        Smoothing,      
        Resampling,     
        Reshaping,
        Rescaling,
        Normalization,  
        FourierTransform
    }
    
    /// <summary>
    /// Identifies which operation this step performs. 
    /// Determines which of the corresponding Options objects must be populated.
    /// </summary>
    [Required]
    public PreprocessingStepType? Type { get; init; }
    
    public ImputationOptions? Imputation { get; init; }
    public SmoothingOptions? Smoothing { get; init; }
    public ResamplingOptions? Resampling { get; init; }
    public NormalizationOptions? Normalization { get; init; }
    public FftOptions? Fft { get; init; }
    public ReshapingOptions? Reshaping { get; init; }
    public RescalingOptions? Rescaling { get; init; }

    /// <summary>
    /// Ensures that the required configuration object is provided based on the selected Type.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Type == PreprocessingStepType.Imputation && Imputation == null)
            yield return new ValidationResult("Imputation options required.", new[] { nameof(Imputation) });
        
        if (Type == PreprocessingStepType.Smoothing && Smoothing == null)
            yield return new ValidationResult("Smoothing options required.", new[] { nameof(Smoothing) });
        
        if (Type == PreprocessingStepType.Resampling && Resampling == null)
            yield return new ValidationResult("Resampling options required.", new[] { nameof(Resampling) });
            
        if (Type == PreprocessingStepType.Normalization && Normalization == null)
            yield return new ValidationResult("Normalization options required.", new[] { nameof(Normalization) });
            
        if (Type == PreprocessingStepType.FourierTransform && Fft == null)
            yield return new ValidationResult("FFT options required.", new[] { nameof(Fft) });

        if (Type == PreprocessingStepType.Reshaping && Reshaping == null)
            yield return new ValidationResult("Reshaping options required.", new[] { nameof(Reshaping) });

        if (Type == PreprocessingStepType.Rescaling && Rescaling == null)
            yield return new ValidationResult("Rescaling options required.", new[] { nameof(Rescaling) });
    }
}

/// <summary>
/// Configuration for cleaning NaN and Infinity values from the data stream.
/// </summary>
public record ImputationOptions
{
    public enum ImputationMethod { ForwardFill, ZeroFill }
    
    /// <summary>
    /// The strategy used to replace invalid values (e.g., carrying the last known value forward vs zeroing it out).
    /// </summary>
    [Required]
    public ImputationMethod? Method { get; init; }
}

/// <summary>
/// Configuration for removing high-frequency noise from time-series data.
/// </summary>
public record SmoothingOptions
{
    public enum SmoothingMethod { MovingAverage, ExponentialSmoothing }
    
    /// <summary>
    /// The algorithmic approach to smoothing the data.
    /// </summary>
    [Required]
    public SmoothingMethod? Method { get; init; }
    
    /// <summary>
    /// The number of historical timesteps to consider when calculating the smoothed value.
    /// </summary>
    [Required]
    [Range(2, int.MaxValue)] 
    public int? WindowSize { get; init; }

    /// <summary>
    /// The decay factor for Exponential Smoothing. A higher gamma discounts older observations faster.
    /// </summary>
    [Range(0.0001, 1)] 
    public float? Gamma { get; init; } = null;
}

/// <summary>
/// Configuration for altering the time-domain frequency of the streaming data.
/// </summary>
public record ResamplingOptions
{
    public enum AggregationFunction { Mean, Min, Max, First, Last }
    
    /// <summary>
    /// The desired output frequency in Hertz.
    /// </summary>
    [Required]
    public double? TargetHz { get; init; }
    
    /// <summary>
    /// The mathematical strategy used to collapse multiple frames into a single frame when downsampling.
    /// </summary>
    public AggregationFunction AggregationFunctionUsed { get; init; } = AggregationFunction.Mean; 
}

/// <summary>
/// Configuration for scaling data to conform to standard statistical distributions.
/// </summary>
public record NormalizationOptions
{
    public enum NormalizationMethod { MinMax, ZScore }
    
    /// <summary>
    /// The statistical strategy used to normalize the values.
    /// </summary>
    [Required]
    public NormalizationMethod? Method { get; init; }
    
    /// <summary>Absolute minimum bound for MinMax scaling. If null, calculated dynamically per chunk.</summary>
    public float? MinBound { get; init; } = null;
    /// <summary>Absolute maximum bound for MinMax scaling. If null, calculated dynamically per chunk.</summary>
    public float? MaxBound { get; init; } = null;
    
    /// <summary>Absolute standard deviation for Z-Score scaling. If null, calculated dynamically per chunk.</summary>
    public float? StdDev { get; init; } = null;
    /// <summary>Absolute mean for Z-Score scaling. If null, calculated dynamically per chunk.</summary>
    public float? Mean { get; init; } = null;
}

/// <summary>
/// Configuration for extracting frequency-domain features via Fast Fourier Transform.
/// </summary>
public record FftOptions
{
    /// <summary>
    /// The size of the sliding time window required before an FFT is computed.
    /// </summary>
    [Required]
    [Range(2, int.MaxValue)]
    public int? WindowSize { get; init; }

    /// <summary>
    /// The number of low-end frequency bins (magnitudes) to pass to the next stage of the pipeline.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? NumberOfFrequenciesToKeep { get; init; }
    
    /// <summary>
    /// Whether to append phase angle calculations alongside the magnitude bins.
    /// </summary>
    public bool IncludePhase { get; init; } = false;
}

/// <summary>
/// Configuration for altering the dimensionality (number of features) of a data frame.
/// </summary>
public record ReshapingOptions
{
    public enum ReshapeMethod { Interpolation, Strict, TruncateOrZeroFill }
    
    /// <summary>
    /// The strategy used to add or remove features to match the target dimension.
    /// </summary>
    [Required]
    public ReshapeMethod? Method { get; init; }
    
    /// <summary>
    /// The desired number of features per frame.
    /// </summary>
    [Required]
    public int? TargetDimension { get; init; }
}

/// <summary>
/// Configuration for applying arbitrary mathematical scalar transformations to the data.
/// </summary>
public record RescalingOptions
{
    public enum RescalingMethod { Linear, Logarithmic, Exponential }
    
    /// <summary>
    /// The function applied to every individual feature element.
    /// </summary>
    [Required]
    public RescalingMethod? Method { get; init; }

    /// <summary>The coefficient applied during Linear scaling (New = Old * Multiplier + Offset).</summary>
    public float Multiplier { get; init; } = 1.0f;
    
    /// <summary>The bias applied during Linear scaling (New = Old * Multiplier + Offset).</summary>
    public float Offset { get; init; } = 0.0f;
}