namespace Maldact.Core.Config.ConfigDefinitions;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Defines the architectural topology and hyperparameters for a target machine learning model.
/// </summary>
public record ModelSpecification : IValidatableObject
{
    public enum AlgorithmType { Gru, Cnn, TreeEnsemble }
    public enum ResultOverlapTimePoolingMethod { Max, Average }
    
    /// <summary>
    /// Gets the human-readable identifier for this specific model configuration.
    /// </summary>
    [Required]
    public string? Name { get; init; }
    
    /// <summary>
    /// Gets the core algorithm engine driving this model.
    /// </summary>
    [Required]
    public AlgorithmType? Algorithm { get; init; }

    /// <summary>
    /// Gets the number of raw features (variables) ingested per discrete timestep.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Input dimension must be at least 1.")]
    public int? InputDimension { get; init; }

    /// <summary>
    /// Gets the number of target classes or continuous values predicted by the model.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? OutputDimension { get; init; }
    
    /// <summary>
    /// Gets the number of sequential timesteps ingested per forward pass.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? WindowSize { get; init; }
    
    /// <summary>
    /// Gets the step size used to advance the sliding window across the time-series sequence.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? WindowStride { get; init; }
    
    /// <summary>
    /// Gets the mathematical strategy used to aggregate overlapping predictions.
    /// </summary>
    public ResultOverlapTimePoolingMethod OverlapPoolingMethod { get; init; } = ResultOverlapTimePoolingMethod.Max;

    /// <summary>
    /// Gets the configuration object applied when the Gru algorithm is selected.
    /// </summary>
    public GruParameters? Gru { get; init; }

    /// <summary>
    /// Gets the configuration object applied when the Cnn algorithm is selected.
    /// </summary>
    public CnnParameters? Cnn { get; init; }

    /// <summary>
    /// Gets the configuration object applied when the TreeEnsemble algorithm is selected.
    /// </summary>
    public TreeParameters? Tree { get; init; }
    
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Algorithm == AlgorithmType.Gru && Gru == null)
            yield return new ValidationResult("Gru parameters must be provided when Algorithm is Gru.", new[] { nameof(Gru) });

        if (Algorithm == AlgorithmType.Cnn && Cnn == null)
            yield return new ValidationResult("Cnn parameters must be provided when Algorithm is Cnn.", new[] { nameof(Cnn) });

        if (Algorithm == AlgorithmType.TreeEnsemble && Tree == null)
            yield return new ValidationResult("Tree parameters must be provided when Algorithm is TreeEnsemble.", new[] { nameof(Tree) });
    }
}

/// <summary>
/// Hyperparameters specific to Gated Recurrent Unit (GRU) networks.
/// </summary>
public record GruParameters
{
    /// <summary>
    /// Gets the number of features within the hidden state sequence.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? HiddenSize { get; init; }
    
    /// <summary>
    /// Gets the number of vertically stacked recurrent layers.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int NumLayers { get; init; }
    
    /// <summary>
    /// Gets the dropout probability applied to the outputs of each GRU layer.
    /// </summary>
    [Required]
    [Range(0.0, double.MaxValue)]
    public double Dropout { get; init; }
}

/// <summary>
/// Hyperparameters specific to Convolutional Neural Networks (CNN).
/// </summary>
public record CnnParameters
{
    /// <summary>
    /// Gets the sequence defining the number of output channels for each consecutive convolutional layer.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one channel size configuration must be provided.")]
    public int[]? ChannelSizes { get; init; } 
                     
    /// <summary>
    /// Gets the spatial size of the 1D convolving kernel.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? KernelSize { get; init; }
    
    /// <summary>
    /// Gets the stride of the convolution along the time axis.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int Stride { get; init; }
}

/// <summary>
/// Hyperparameters specific to classical tree-based ensemble models.
/// </summary>
public record TreeParameters
{
    public enum TreeType { RandomForest, XgBoost }

    /// <summary>
    /// Gets the specific tree algorithm utilized for classification.
    /// </summary>
    [Required]
    public TreeType? EnsembleType { get; init; }
    
    /// <summary>
    /// Gets the total number of decision trees constructed within the forest.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int NumberOfTrees { get; init; } = 100;
    
    /// <summary>
    /// Gets the maximum allowed traversal depth for any individual tree.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int MaxDepth { get; init; } = 6;
}