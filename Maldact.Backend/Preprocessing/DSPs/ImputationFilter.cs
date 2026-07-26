using Maldact.Core.Data;
using Maldact.Core.Preprocessing;
using Maldact.Core.Streaming;

namespace Maldact.Backend.Preprocessing.DSPs;

/// <summary>
/// Cleans streaming data by replacing NaN (Not-a-Number) and Infinity values 
/// with safe numerical fallbacks.
/// </summary>
public class ImputationFilter : IDataPreprocessor
{
    /// <summary>
    /// The strategy used to replace invalid numerical values.
    /// </summary>
    public enum ImputationMethod 
    { 
        /// <summary>Replaces invalid values with exactly 0.0f.</summary>
        ZeroFill, 
        
        /// <summary>Replaces invalid values with the last observed valid value for that feature.</summary>
        ForwardFill 
    }

    /// <summary>
    /// The number of features per time step in the input stream.
    /// </summary>
    public int InputDimension { get; }
    
    /// <summary>
    /// The number of features per time step after processing. Matches InputDimension.
    /// </summary>
    public int OutputDimension { get; }

    private readonly ImputationMethod _method;
    private readonly float[]? _lastKnownValues;

    /// <summary>
    /// Initializes a new instance of the ImputationFilter.
    /// </summary>
    /// <param name="method">The imputation strategy to apply.</param>
    /// <param name="dimension">The number of features in the data stream.</param>
    public ImputationFilter(ImputationMethod method, int dimension)
    {
        _method = method;
        InputDimension = OutputDimension = dimension;

        if (_method == ImputationMethod.ForwardFill)
        {
            _lastKnownValues = new float[InputDimension];
            // defaults to zero if the very first frame contains nan
            Array.Fill(_lastKnownValues, 0f); 
        }
    }

    /// <summary>
    /// Processes a sequential chunk of time-series data, stripping out invalid floats using pooled memory.
    /// </summary>
    /// <param name="chunk">The zero-allocation data carrier to be processed.</param>
    public void Process(PipelineChunk chunk)
    {
        ReadOnlySpan<float> input = chunk.CurrentData;
        if (input.Length == 0) return;

        int timeSteps = input.Length / InputDimension;
        Span<float> output = chunk.AdvancePreprocessingStep(timeSteps * OutputDimension);

        // hot path 1: branchless forward fill
        if (_method == ImputationMethod.ForwardFill)
        {
            for (int t = 0; t < timeSteps; t++)
            {
                int offset = t * InputDimension;
                for (int f = 0; f < InputDimension; f++)
                {
                    float val = input[offset + f];
                    
                    if (float.IsNaN(val) || float.IsInfinity(val))
                    {
                        output[offset + f] = _lastKnownValues![f];
                    }
                    else
                    {
                        output[offset + f] = val;
                        _lastKnownValues![f] = val;
                    }
                }
            }
        }
        // hot path 2: branchless zero fill
        else 
        {
            for (int i = 0; i < input.Length; i++)
            {
                float val = input[i];
                output[i] = float.IsNaN(val) || float.IsInfinity(val) ? 0f : val;
            }
        }
    }
}