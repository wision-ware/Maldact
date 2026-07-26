using Maldact.Core.Data;
using Maldact.Core.Preprocessing;
using Maldact.Core.Streaming;

namespace Maldact.Backend.Preprocessing.DSPs;

/// <summary>
/// Normalizes streaming data by standardizing features to have a mean of 0 and a standard deviation of 1.
/// Can use global distribution statistics or dynamically calculate them per chunk.
/// </summary>
public class ZScoreNormalizer : IDataPreprocessor
{
    /// <summary>
    /// The number of features per time step in the input stream.
    /// </summary>
    public int InputDimension { get; }
    
    /// <summary>
    /// The number of features per time step after processing. Matches InputDimension.
    /// </summary>
    public int OutputDimension { get; }
    
    private readonly float[]? _globalMeans;
    private readonly float[]? _globalStdDevs;

    /// <summary>
    /// Initializes a new instance of the ZScoreNormalizer.
    /// </summary>
    /// <param name="dimension">The number of features in the data stream.</param>
    /// <param name="globalMeans">Optional array of absolute means per feature. If null, calculated per chunk.</param>
    /// <param name="globalStdDevs">Optional array of absolute standard deviations per feature. If null, calculated per chunk.</param>
    /// <exception cref="ArgumentException">Thrown if provided bounds arrays do not perfectly match the dimension.</exception>
    public ZScoreNormalizer(int dimension, float[]? globalMeans = null, float[]? globalStdDevs = null)
    {
        InputDimension = OutputDimension = dimension;
        
        if (globalMeans is not null) 
            if (globalMeans.Length != dimension) throw new ArgumentException(
                $"Argument parity mismatch: {nameof(dimension)} does not match {nameof(globalMeans)} length!");
        
        if (globalStdDevs is not null) 
            if (globalStdDevs.Length != dimension) throw new ArgumentException(
                $"Argument parity mismatch: {nameof(dimension)} does not match {nameof(globalStdDevs)} length!");
        
        _globalMeans = globalMeans;
        _globalStdDevs = globalStdDevs;
    }

    /// <summary>
    /// Processes a sequential chunk of data, applying standard scaling to each feature in-place via pooled memory.
    /// </summary>
    /// <param name="chunk">The zero-allocation data carrier to be processed.</param>
    public void Process(PipelineChunk chunk)
    {
        ReadOnlySpan<float> input = chunk.CurrentData;
        if (input.Length == 0) return;

        int timeSteps = input.Length / InputDimension;
        Span<float> output = chunk.AdvancePreprocessingStep(timeSteps * OutputDimension);

        // precompute stats to prevent cache thrashing in main loop
        float[] means = new float[InputDimension];
        float[] invStdDevs = new float[InputDimension];

        for (int f = 0; f < InputDimension; f++)
        {
            float mean = _globalMeans?[f] ?? CalculateChunkMean(input, f, timeSteps);
            float stdDev = _globalStdDevs?[f] ?? CalculateChunkStdDev(input, f, timeSteps, mean);
            
            means[f] = mean;
            // div by zero protection + invert for faster multiplication later
            invStdDevs[f] = stdDev == 0f ? 0f : 1f / stdDev;
        }

        // hot path: cache-friendly linear memory access
        for (int t = 0; t < timeSteps; t++)
        {
            int offset = t * InputDimension;
            for (int f = 0; f < InputDimension; f++)
            {
                // scaling via multiplication
                if (invStdDevs[f] == 0f)
                {
                    output[offset + f] = 0f;
                }
                else
                {
                    output[offset + f] = (input[offset + f] - means[f]) * invStdDevs[f];
                }
            }
        }
    }
    
    private float CalculateChunkMean(ReadOnlySpan<float> input, int featureIdx, int timeSteps)
    {
        float sum = 0f;
        for (int t = 0; t < timeSteps; t++)
        {
            sum += input[t * InputDimension + featureIdx];
        }
        return sum / timeSteps;
    }
    
    private float CalculateChunkStdDev(ReadOnlySpan<float> input, int featureIdx, int timeSteps, float mean)
    {
        float sqSum = 0f;
        for (int t = 0; t < timeSteps; t++)
        {
            float diff = input[t * InputDimension + featureIdx] - mean;
            sqSum += diff * diff;
        }
        return MathF.Sqrt(sqSum / timeSteps);
    }
}