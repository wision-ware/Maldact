using Maldact.Core.Data;
using Maldact.Core.Preprocessing;
using Maldact.Core.Streaming;

namespace Maldact.Backend.Preprocessing.DSPs;

/// <summary>
/// Normalizes streaming data by scaling values to a proportional range between 0 and 1.
/// Can use global bounds or dynamically calculate bounds per chunk.
/// </summary>
public class MinMaxNormalizer : IDataPreprocessor
{
    /// <summary>
    /// The number of features per time step in the input stream.
    /// </summary>
    public int InputDimension { get; }
    
    /// <summary>
    /// The number of features per time step after processing. Matches InputDimension.
    /// </summary>
    public int OutputDimension { get; }
    
    private readonly float[]? _globalMins;
    private readonly float[]? _globalMaxes;

    /// <summary>
    /// Initializes a new instance of the MinMaxNormalizer.
    /// </summary>
    /// <param name="dimension">The number of features in the data stream.</param>
    /// <param name="globalMins">Optional array of absolute minimums per feature. If null, calculated per chunk.</param>
    /// <param name="globalMaxes">Optional array of absolute maximums per feature. If null, calculated per chunk.</param>
    /// <exception cref="ArgumentException">Thrown if provided bounds arrays do not perfectly match the dimension.</exception>
    public MinMaxNormalizer(int dimension, float[]? globalMins = null, float[]? globalMaxes = null)
    {
        InputDimension = OutputDimension = dimension;
        
        if (globalMins is not null) 
            if (globalMins.Length != dimension) throw new ArgumentException(
                $"Argument parity mismatch: {nameof(dimension)} does not match {nameof(globalMins)} length!");
        
        if (globalMaxes is not null) 
            if (globalMaxes.Length != dimension) throw new ArgumentException(
                $"Argument parity mismatch: {nameof(dimension)} does not match {nameof(globalMaxes)} length!");
        
        _globalMins = globalMins;
        _globalMaxes = globalMaxes;
    }

    /// <summary>
    /// Processes a sequential chunk of data, applying min-max scaling to each feature via pooled memory.
    /// </summary>
    /// <param name="chunk">The zero-allocation data carrier to be processed.</param>
    public void Process(PipelineChunk chunk)
    {
        ReadOnlySpan<float> input = chunk.CurrentData;
        if (input.Length == 0) return;

        int timeSteps = input.Length / InputDimension;
        Span<float> output = chunk.AdvancePreprocessingStep(timeSteps * OutputDimension);

        // precompute local bounds to prevent cache thrashing in main loop
        float[] mins = new float[InputDimension];
        float[] inverseRanges = new float[InputDimension];

        for (int f = 0; f < InputDimension; f++)
        {
            float min = _globalMins?[f] ?? CalculateChunkMin(input, f, timeSteps);
            float max = _globalMaxes?[f] ?? CalculateChunkMax(input, f, timeSteps);
            float range = max - min;
            
            mins[f] = min;
            // div by zero protection + invert for faster multiplication later
            inverseRanges[f] = range == 0f ? 0f : 1f / range; 
        }

        // hot path: cache-friendly linear memory access 
        for (int t = 0; t < timeSteps; t++)
        {
            int offset = t * InputDimension;
            for (int f = 0; f < InputDimension; f++)
            {
                // scaling via multiplication
                if (inverseRanges[f] == 0f) 
                {
                    output[offset + f] = 0f;
                }
                else 
                {
                    output[offset + f] = (input[offset + f] - mins[f]) * inverseRanges[f];
                }
            }
        }
    }

    private float CalculateChunkMin(ReadOnlySpan<float> input, int featureIdx, int timeSteps)
    {
        float min = float.MaxValue;
        for (int t = 0; t < timeSteps; t++) 
        {
            float val = input[t * InputDimension + featureIdx];
            if (val < min) min = val;
        }
        return min;
    }

    private float CalculateChunkMax(ReadOnlySpan<float> input, int featureIdx, int timeSteps)
    {
        float max = float.MinValue;
        for (int t = 0; t < timeSteps; t++) 
        {
            float val = input[t * InputDimension + featureIdx];
            if (val > max) max = val;
        }
        return max;
    }
}