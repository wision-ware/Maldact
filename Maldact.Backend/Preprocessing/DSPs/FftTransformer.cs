using Maldact.Core.Streaming;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.Data;
using Maldact.Core.Preprocessing;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace Maldact.Backend.Preprocessing.DSPs;

/// <summary>
/// Applies a rolling Fast Fourier Transform (FFT) over a sliding window of time-series data.
/// Extracts frequency magnitudes and optionally phase data for downstream processing.
/// </summary>
public class FftTransformer : IDataPreprocessor
{
    public int InputDimension { get; }
    public int OutputDimension { get; }

    private readonly int _windowSize;
    private readonly int _frequenciesToKeep;
    private readonly bool _includePhase;
    
    // State arrays allocated exactly once during construction to prevent GC thrashing
    private readonly float[][] _featureBuffers;
    private readonly Complex32[] _reusableFftBuffer; 
    
    private int _bufferPosition;
    private int _bufferCount;

    /// <summary>
    /// Initializes a new instance of the FftTransformer.
    /// </summary>
    /// <param name="inputDimension">The number of features in the input stream.</param>
    /// <param name="windowSize">The size of the sliding window for the FFT.</param>
    /// <param name="frequenciesToKeep">The number of low-end frequency bins to retain.</param>
    /// <param name="includePhase">Whether to include phase angles alongside magnitudes.</param>
    public FftTransformer(int inputDimension, int windowSize, int frequenciesToKeep, bool includePhase)
    {
        _windowSize = windowSize;
        _frequenciesToKeep = frequenciesToKeep;
        _includePhase = includePhase;
        InputDimension = inputDimension;
        
        OutputDimension = inputDimension * _frequenciesToKeep * (_includePhase ? 2 : 1);
        
        _reusableFftBuffer = new Complex32[_windowSize];
        _featureBuffers = new float[InputDimension][];
        
        for (int i = 0; i < InputDimension; i++)
        {
            _featureBuffers[i] = new float[_windowSize];
        }
    }

    /// <summary>
    /// Processes a chunk of data via pooled memory, applying a sliding window FFT to each timestep.
    /// Yields zeros until the sliding window is full.
    /// </summary>
    /// <param name="chunk">The zero-allocation data carrier to be processed.</param>
    public void Process(PipelineChunk chunk)
    {
        ReadOnlySpan<float> input = chunk.CurrentData;
        if (input.Length == 0) return;

        int timeSteps = input.Length / InputDimension;
        Span<float> output = chunk.AdvancePreprocessingStep(timeSteps * OutputDimension);
        
        // Ensure zeros are returned for unfilled window frames
        output.Clear(); 

        int outFeaturesPerInput = _frequenciesToKeep * (_includePhase ? 2 : 1);

        for (int t = 0; t < timeSteps; t++)
        {
            int inOffset = t * InputDimension;
            int outOffsetTime = t * OutputDimension;

            // push new values into circular buffer
            for (int f = 0; f < InputDimension; f++)
            {
                _featureBuffers[f][_bufferPosition] = input[inOffset + f];
            }

            _bufferCount = Math.Min(_bufferCount + 1, _windowSize);

            // only compute fft if buffer is completely full
            if (_bufferCount == _windowSize)
            {
                for (int f = 0; f < InputDimension; f++)
                {
                    for (int i = 0; i < _windowSize; i++)
                    {
                        int index = (_bufferPosition + 1 + i) % _windowSize;
                        _reusableFftBuffer[i] = new Complex32(_featureBuffers[f][index], 0f);
                    }

                    Fourier.Forward(_reusableFftBuffer, FourierOptions.Matlab);

                    int finalOutOffset = outOffsetTime + (f * outFeaturesPerInput);
                    
                    for (int k = 0; k < _frequenciesToKeep; k++)
                    {
                        output[finalOutOffset + k] = _reusableFftBuffer[k].Magnitude;
                        
                        if (_includePhase)
                        {
                            output[finalOutOffset + _frequenciesToKeep + k] = _reusableFftBuffer[k].Phase;
                        }
                    }
                }
            }

            _bufferPosition = (_bufferPosition + 1) % _windowSize;
        }
    }
}
