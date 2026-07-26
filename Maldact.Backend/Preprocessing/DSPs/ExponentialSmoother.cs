using Maldact.Core.Data;
using Maldact.Core.Preprocessing;
using Maldact.Core.Streaming;

namespace Maldact.Backend.Preprocessing.DSPs;

/// <summary>
/// Applies Exponential Moving Average (EMA) smoothing to a continuous stream of data chunks.
/// Maintains state between chunks to ensure seamless temporal smoothing.
/// </summary>
public class ExponentialSmoother : IDataPreprocessor
{
    /// <summary>
    /// The expected number of features per time step in the input stream.
    /// </summary>
    public int InputDimension { get; }
    
    /// <summary>
    /// The number of features per time step after processing. Matches InputDimension.
    /// </summary>
    public int OutputDimension { get; }

    private readonly float _gamma;
    private readonly float _oneMinusGamma;

    private readonly float[] _previousSmoothed;
    private bool _isInitialized = false;

    /// <summary>
    /// Initializes a new instance of the ExponentialSmoother.
    /// </summary>
    /// <param name="dimension">The number of features in each time step.</param>
    /// <param name="windowSize">The effective window size used to calculate the default gamma factor.</param>
    /// <param name="gamma">An optional explicit smoothing factor between 0 and 1. If null, calculated from windowSize.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if gamma is not between 0 and 1.</exception>
    public ExponentialSmoother(int dimension, int windowSize, float? gamma = null)
    {
        if (gamma is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(gamma), "gamma must be between 0 and 1");

        InputDimension = OutputDimension = dimension;
        
        _gamma = gamma ?? 2f / (windowSize + 1f);
        _oneMinusGamma = 1f - _gamma;

        _previousSmoothed = new float[InputDimension];
    }

    /// <summary>
    /// Processes a sequential chunk of time-series data, applying exponential smoothing via zero-allocation memory pooling.
    /// </summary>
    /// <param name="chunk">The zero-allocation data carrier to be processed.</param>
    public void Process(PipelineChunk chunk)
    {
        ReadOnlySpan<float> input = chunk.CurrentData;
        if (input.Length == 0) return;

        int timeSteps = input.Length / InputDimension;
        Span<float> output = chunk.AdvancePreprocessingStep(timeSteps * OutputDimension);

        int startIndex = 0;

        // branchless unswitching: handle absolute first frame uniquely
        if (!_isInitialized)
        {
            for (int f = 0; f < InputDimension; f++)
            {
                float val = input[f];
                output[f] = val;
                _previousSmoothed[f] = val;
            }
            _isInitialized = true;
            startIndex = 1;
        }

        // hot path: linear memory access, pure math, zero branching
        for (int t = startIndex; t < timeSteps; t++)
        {
            int offset = t * InputDimension;
            for (int f = 0; f < InputDimension; f++)
            {
                float val = input[offset + f];
                float smoothedValue = (_gamma * val) + (_oneMinusGamma * _previousSmoothed[f]);
                
                output[offset + f] = smoothedValue;
                _previousSmoothed[f] = smoothedValue;
            }
        }
    }
}