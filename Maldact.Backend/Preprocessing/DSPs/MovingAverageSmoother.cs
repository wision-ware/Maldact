using Maldact.Core.Data;
using Maldact.Core.Preprocessing;
using Maldact.Core.Streaming;

namespace Maldact.Backend.Preprocessing.DSPs;

/// <summary>
/// Applies a Simple Moving Average (SMA) filter over a sliding window.
/// Maintains state between chunks to ensure seamless continuous smoothing.
/// </summary>
public class MovingAverageSmoother : IDataPreprocessor
{
    /// <summary>
    /// The number of features per time step in the input stream.
    /// </summary>
    public int InputDimension { get; }
    
    /// <summary>
    /// The number of features per time step after processing. Matches InputDimension.
    /// </summary>
    public int OutputDimension { get; }

    private readonly int _windowSize;
    
    // Flattened 1D array to maximize cache locality (no jagged array dereferencing)
    private readonly float[] _history;
    private readonly double[] _sums;
    
    private int _head;
    private int _count;

    /// <summary>
    /// Initializes a new instance of the MovingAverageSmoother.
    /// </summary>
    /// <param name="dimension">The number of features in the data stream.</param>
    /// <param name="windowSize">The number of historical frames to average together.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if window size is zero or less.</exception>
    public MovingAverageSmoother(int dimension, int windowSize)
    {
        if (windowSize <= 0) 
            throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size must be greater than zero.");

        InputDimension = OutputDimension = dimension;
        _windowSize = windowSize;

        _history = new float[_windowSize * InputDimension];
        _sums = new double[InputDimension];
    }

    /// <summary>
    /// Processes a sequential chunk of data, applying a moving average to each feature via pooled memory.
    /// </summary>
    /// <param name="chunk">The zero-allocation data carrier to be processed.</param>
    public void Process(PipelineChunk chunk)
    {
        ReadOnlySpan<float> input = chunk.CurrentData;
        if (input.Length == 0) return;

        int timeSteps = input.Length / InputDimension;
        Span<float> output = chunk.AdvancePreprocessingStep(timeSteps * OutputDimension);

        for (int t = 0; t < timeSteps; t++)
        {
            int offset = t * InputDimension;
            
            // initialization checks and inverse divisor
            bool isFull = _count == _windowSize;
            int currentDivisor = isFull ? _windowSize : _count + 1;
            double inverseDivisor = 1.0 / currentDivisor;

            // hot path: single merged pass per feature
            for (int f = 0; f < InputDimension; f++)
            {
                float val = input[offset + f];

                // remove oldest value from running sum if window is full
                if (isFull)
                {
                    _sums[f] -= _history[_head * InputDimension + f];
                }

                // add new value to history and sum
                _history[_head * InputDimension + f] = val;
                _sums[f] += val;
                
                // multiply by inverse
                output[offset + f] = (float)(_sums[f] * inverseDivisor);
            }

            // advance circular buffer pointers
            _head = (_head + 1) % _windowSize;
            if (!isFull) _count++;
        }
    }
}