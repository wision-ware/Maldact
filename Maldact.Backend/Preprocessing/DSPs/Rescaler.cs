using Maldact.Core.Data;
using Maldact.Core.Preprocessing;
using Maldact.Core.Streaming;

namespace Maldact.Backend.Preprocessing.DSPs;

/// <summary>
/// Applies a custom vectorized mathematical function to each frame of the streaming data.
/// Useful for arbitrary scaling, logarithmic transforms, or custom clip functions.
/// </summary>
public class Rescaler : IDataPreprocessor
{
    /// <summary>
    /// Defines a custom operation that mutates a stack-allocated feature frame in-place.
    /// </summary>
    /// <param name="frame">A contiguous memory span representing a single time step.</param>
    public delegate void VectorOperation(Span<float> frame);

    /// <summary>
    /// The number of features per time step in the input stream.
    /// </summary>
    public int InputDimension { get; }
    
    /// <summary>
    /// The number of features per time step after processing. Matches InputDimension.
    /// </summary>
    public int OutputDimension { get; }
    
    private readonly VectorOperation _vectorizedRescalingFunction;

    /// <summary>
    /// Initializes a new instance of the Rescaler.
    /// </summary>
    /// <param name="dimension">The number of features in the data stream.</param>
    /// <param name="vectorizedRescalingFunction">A delegate that mutates a 1D feature span in-place.</param>
    public Rescaler(int dimension, VectorOperation vectorizedRescalingFunction)
    {
        ArgumentNullException.ThrowIfNull(vectorizedRescalingFunction);
        
        InputDimension = OutputDimension = dimension;
        _vectorizedRescalingFunction = vectorizedRescalingFunction;
    }
    
    /// <summary>
    /// Processes a sequential chunk of data via zero-allocation pooling, applying the custom rescaling function.
    /// </summary>
    /// <param name="chunk">The zero-allocation data carrier to be processed.</param>
    public void Process(PipelineChunk chunk)
    {
        ReadOnlySpan<float> input = chunk.CurrentData;
        if (input.Length == 0) return;

        int timeSteps = input.Length / InputDimension;
        Span<float> output = chunk.AdvancePreprocessingStep(timeSteps * OutputDimension);

        // AdvancePreprocessingStep gives us an uninitialized array, so we must copy the input first
        input.CopyTo(output);

        for (int t = 0; t < timeSteps; t++)
        {
            // Slice the flat array into a single 1D frame and pass it to the user's delegate
            Span<float> frame = output.Slice(t * InputDimension, InputDimension);
            _vectorizedRescalingFunction(frame);
        }
    }
}