using Maldact.Core.Data;
using Maldact.Core.Preprocessing;
using Maldact.Core.Streaming;
namespace Maldact.Backend.Preprocessing.Pipelines;

/// <summary>
/// Chains multiple data preprocessors together into a single sequential pipeline.
/// The output of one step seamlessly becomes the input to the next via the PipelineChunk state machine.
/// </summary>
public class LinearPipelineCompositePreprocessor : IDataPreprocessor 
{
    private readonly IDataPreprocessor[] _steps;
    
    /// <summary>
    /// The number of features expected by the first step in the pipeline.
    /// </summary>
    public int InputDimension { get; }
    
    /// <summary>
    /// The resulting dimensionality after the final step in the pipeline is applied.
    /// </summary>
    public int OutputDimension { get; }

    /// <summary>
    /// Initializes a new instance of the LinearPipelineCompositePreprocessor.
    /// </summary>
    /// <param name="steps">An ordered list of preprocessors to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown if the steps list is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the pipeline contains zero steps.</exception>
    /// <exception cref="InvalidOperationException">Thrown if adjacent pipeline steps have dimension mismatches.</exception>
    public LinearPipelineCompositePreprocessor(IList<IDataPreprocessor> steps) 
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count == 0) throw new ArgumentException("Pipeline must contain at least one step.", nameof(steps));

        _steps = steps.ToArray();
        
        InputDimension = _steps[0].InputDimension;
        OutputDimension = _steps[^1].OutputDimension;

        // fail-fast: ensure all pipeline links mathematically connect
        for (int i = 0; i < _steps.Length - 1; i++)
        {
            if (_steps[i].OutputDimension != _steps[i + 1].InputDimension)
            {
                throw new InvalidOperationException(
                    $"Pipeline dimension mismatch at step {i} to {i + 1}. " +
                    $"Output dimension {_steps[i].OutputDimension} does not match input dimension {_steps[i + 1].InputDimension}.");
            }
        }
    }
    
    /// <summary>
    /// Processes a sequential chunk of data by passing it through the entire pipeline chain.
    /// Orchestrates zero-allocation memory swaps internally.
    /// </summary>
    /// <param name="chunk">The zero-allocation data carrier to be processed.</param>
    public void Process(PipelineChunk chunk) 
    {
        if (chunk.CurrentData.Length == 0) return;

        // execute steps sequentially, each step mutates and advances the chunk's internal state
        foreach (var step in _steps) 
        {
            step.Process(chunk);
        }
    }
}
