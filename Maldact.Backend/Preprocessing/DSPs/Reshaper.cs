using Maldact.Core.Data;
using Maldact.Core.Preprocessing;
using Maldact.Core.Streaming;

namespace Maldact.Backend.Preprocessing.DSPs;

/// <summary>
/// Reshapes the feature dimension of a streaming chunk to match a target dimensionality.
/// </summary>
public class Reshaper : IDataPreprocessor
{
    /// <summary>
    /// The strategy used to align mismatched dimensions.
    /// </summary>
    public enum ReshapeMethod 
    { 
        /// <summary>Stretches or squashes the vector using linear interpolation.</summary>
        Interpolation, 
        
        /// <summary>Throws an exception if dimensions do not perfectly match.</summary>
        Strict, 
        
        /// <summary>Copies data up to the target length, padding with zeroes if target is larger.</summary>
        TruncateOrZeroFill 
    }

    private readonly ReshapeMethod _method;

    /// <summary>
    /// The expected number of features per time step in the input stream.
    /// </summary>
    public int InputDimension { get; }
    
    /// <summary>
    /// The resulting dimensionality after reshaping.
    /// </summary>
    public int OutputDimension { get; }

    // cached interpolation maps
    private readonly int[]? _leftIndices;
    private readonly int[]? _rightIndices;
    private readonly float[]? _weights;

    /// <summary>
    /// Initializes a new instance of the Reshaper.
    /// </summary>
    /// <param name="inputDimension">The original feature count.</param>
    /// <param name="targetDimension">The desired feature count.</param>
    /// <param name="method">The strategy to apply if the dimensions differ.</param>
    public Reshaper(int inputDimension, int targetDimension, ReshapeMethod method)
    {
        _method = method;
        InputDimension = inputDimension;
        OutputDimension = targetDimension;

        if (_method == ReshapeMethod.Interpolation && InputDimension != OutputDimension)
        {
            _leftIndices = new int[OutputDimension];
            _rightIndices = new int[OutputDimension];
            _weights = new float[OutputDimension];

            if (OutputDimension == 1 || InputDimension == 1) return;

            float ratio = (float)(InputDimension - 1) / (OutputDimension - 1);

            for (int j = 0; j < OutputDimension; j++)
            {
                float sourceContinuousIndex = j * ratio;
                _leftIndices[j] = (int)MathF.Floor(sourceContinuousIndex);
                _rightIndices[j] = Math.Min(_leftIndices[j] + 1, InputDimension - 1);
                _weights[j] = sourceContinuousIndex - _leftIndices[j];
            }
        }
    }

    /// <summary>
    /// Processes a sequential chunk of data, reshaping the feature vectors directly in memory.
    /// </summary>
    /// <param name="chunk">The zero-allocation data carrier to be processed.</param>
    public void Process(PipelineChunk chunk)
    {
        ReadOnlySpan<float> input = chunk.CurrentData;
        if (input.Length == 0) return;

        if (InputDimension == OutputDimension) return; // bypass if perfectly aligned

        if (_method == ReshapeMethod.Strict)
        {
            throw new InvalidOperationException(
                $"Strict reshape failed. Expected dimension {OutputDimension}, but received {InputDimension}.");
        }

        int timeSteps = input.Length / InputDimension;
        Span<float> output = chunk.AdvancePreprocessingStep(timeSteps * OutputDimension);

        if (_method == ReshapeMethod.TruncateOrZeroFill)
        {
            int copyLength = Math.Min(InputDimension, OutputDimension);
            output.Clear(); // Ensure padding is zeroed

            for (int t = 0; t < timeSteps; t++)
            {
                ReadOnlySpan<float> sourceSlice = input.Slice(t * InputDimension, copyLength);
                Span<float> targetSlice = output.Slice(t * OutputDimension, copyLength);
                sourceSlice.CopyTo(targetSlice);
            }
        }
        else if (_method == ReshapeMethod.Interpolation)
        {
            for (int t = 0; t < timeSteps; t++)
            {
                ReadOnlySpan<float> sourceSlice = input.Slice(t * InputDimension, InputDimension);
                Span<float> targetSlice = output.Slice(t * OutputDimension, OutputDimension);
                ApplyPrecomputedInterpolation(sourceSlice, targetSlice);
            }
        }
    }

    private void ApplyPrecomputedInterpolation(ReadOnlySpan<float> source, Span<float> target)
    {
        if (OutputDimension == 1)
        {
            target[0] = source[0];
            return;
        }
        
        if (InputDimension == 1)
        {
            target.Fill(source[0]);
            return;
        }

        for (int j = 0; j < OutputDimension; j++)
        {
            target[j] = source[_leftIndices![j]] + 
                       (source[_rightIndices![j]] - source[_leftIndices[j]]) * _weights![j];
        }
    }
}