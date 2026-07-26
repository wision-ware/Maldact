using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.Preprocessing;
using Maldact.Core.Streaming;
using System.Collections.Generic;
using Maldact.Core.Data;

namespace Maldact.Backend.Preprocessing.DSPs;

/// <summary>
/// Resamples streaming time-series data to a new target frequency.
/// Supports fractional upsampling via linear interpolation and downsampling via aggregation.
/// </summary>
public class Resampler : IDataPreprocessor
{
    /// <summary>
    /// Defines the mathematical strategy used when downsampling multiple frames into a single bucket.
    /// </summary>
    public enum AggregationFunction { Mean, Max, Min, First, Last }
    
    /// <summary>
    /// The number of features per time step in the input stream.
    /// </summary>
    public int InputDimension { get; }
    
    /// <summary>
    /// The number of features per time step after processing. Matches InputDimension.
    /// </summary>
    public int OutputDimension { get; }
    
    private readonly double _sourceRateHz;
    private readonly double _targetRateHz;
    private readonly AggregationFunction _aggFunc;
    private readonly double _targetStepInSourceUnits;

    private double _logicalTime = 0.0;
    private double _nextTargetTime = 0.0;
    
    // State buffers allocated once to prevent GC allocations across chunks
    private readonly float[] _previousSample;
    private readonly float[] _aggBuffer;
    
    private bool _hasPreviousSample = false;
    private long _currentBucketIndex = 0;
    private int _aggCount = 0;

    /// <summary>
    /// Initializes a new instance of the Resampler.
    /// </summary>
    /// <param name="dimension">The number of features in the data stream.</param>
    /// <param name="sourceRateHz">The original sampling frequency of the incoming data.</param>
    /// <param name="targetRateHz">The desired sampling frequency.</param>
    /// <param name="aggFunc">The aggregation strategy to use if downsampling is required.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if frequencies are zero or negative.</exception>
    public Resampler(int dimension, double sourceRateHz, double targetRateHz, AggregationFunction aggFunc)
    {
        if (targetRateHz <= 0 || sourceRateHz <= 0) 
            throw new ArgumentOutOfRangeException("Frequencies must be greater than zero.");

        _sourceRateHz = sourceRateHz;
        _targetRateHz = targetRateHz;
        _aggFunc = aggFunc;
        _targetStepInSourceUnits = _sourceRateHz / _targetRateHz;
        
        InputDimension = OutputDimension = dimension;
        _previousSample = new float[InputDimension];
        _aggBuffer = new float[InputDimension];
        ResetAggBuffer();
    }

    /// <summary>
    /// Processes a sequential chunk of data via pooled memory, applying either upsampling or downsampling.
    /// </summary>
    /// <param name="chunk">The zero-allocation data carrier to be processed.</param>
    public void Process(PipelineChunk chunk)
    {
        ReadOnlySpan<float> input = chunk.CurrentData;
        if (input.Length == 0) return;

        if (Math.Abs(_sourceRateHz - _targetRateHz) < 0.0001) return;

        if (_targetRateHz < _sourceRateHz) DownsampleFractional(chunk, input);
        else UpsampleFractional(chunk, input);
    }

    private void DownsampleFractional(PipelineChunk chunk, ReadOnlySpan<float> input)
    {
        int timeSteps = input.Length / InputDimension;
        
        // DRY RUN: Calculate exactly how many frames will be emitted to rent the correct buffer size
        int outputFrames = 0;
        double tempTime = _logicalTime;
        long tempBucket = _currentBucketIndex;
        int tempCount = _aggCount;
        
        for (int t = 0; t < timeSteps; t++)
        {
            long tBucket = (long)(tempTime / _targetStepInSourceUnits);
            if (tBucket > tempBucket && tempCount > 0)
            {
                outputFrames++;
                tempCount = 0;
                tempBucket = tBucket;
            }
            tempCount++;
            tempTime += 1.0;
        }

        Span<float> output = chunk.AdvancePreprocessingStep(outputFrames * OutputDimension);
        int outIndex = 0;

        // HOT PATH: Actually perform the aggregation and emission
        for (int t = 0; t < timeSteps; t++)
        {
            long targetBucketIndex = (long)(_logicalTime / _targetStepInSourceUnits);
            ReadOnlySpan<float> frame = input.Slice(t * InputDimension, InputDimension);

            if (targetBucketIndex > _currentBucketIndex && _aggCount > 0)
            {
                EmitAggregatedBucket(output.Slice(outIndex * OutputDimension, OutputDimension));
                outIndex++;
                ResetAggBuffer();
                _currentBucketIndex = targetBucketIndex;
            }

            AccumulateIntoBucket(frame);
            _logicalTime += 1.0;
        }
    }

    private void UpsampleFractional(PipelineChunk chunk, ReadOnlySpan<float> input)
    {
        int timeSteps = input.Length / InputDimension;

        // DRY RUN: Calculate exact output frames
        int outputFrames = 0;
        double tempTime = _logicalTime;
        double tempNextTime = _nextTargetTime;
        bool tempHasPrev = _hasPreviousSample;
        
        for (int t = 0; t < timeSteps; t++)
        {
            if (!tempHasPrev)
            {
                tempHasPrev = true;
                outputFrames++;
                tempNextTime += _targetStepInSourceUnits;
            }
            else
            {
                while (tempNextTime <= tempTime)
                {
                    outputFrames++;
                    tempNextTime += _targetStepInSourceUnits;
                }
            }
            tempTime += 1.0;
        }

        Span<float> output = chunk.AdvancePreprocessingStep(outputFrames * OutputDimension);
        int outIndex = 0;

        // HOT PATH
        for (int t = 0; t < timeSteps; t++)
        {
            ReadOnlySpan<float> frame = input.Slice(t * InputDimension, InputDimension);

            if (!_hasPreviousSample)
            {
                frame.CopyTo(_previousSample);
                frame.CopyTo(output.Slice(outIndex * OutputDimension, OutputDimension));
                outIndex++;
                _hasPreviousSample = true;
                _nextTargetTime += _targetStepInSourceUnits;
            }
            else
            {
                while (_nextTargetTime <= _logicalTime)
                {
                    double ratio = _nextTargetTime - (_logicalTime - 1.0);
                    Interpolate(_previousSample, frame, (float)ratio, output.Slice(outIndex * OutputDimension, OutputDimension));
                    outIndex++;
                    _nextTargetTime += _targetStepInSourceUnits;
                }
                frame.CopyTo(_previousSample);
            }
            _logicalTime += 1.0;
        }
    }

    private void AccumulateIntoBucket(ReadOnlySpan<float> frame)
    {
        for (int f = 0; f < InputDimension; f++)
        {
            float val = frame[f];
            switch (_aggFunc)
            {
                case AggregationFunction.Mean: _aggBuffer[f] += val; break;
                case AggregationFunction.Max: if (val > _aggBuffer[f]) _aggBuffer[f] = val; break;
                case AggregationFunction.Min: if (val < _aggBuffer[f]) _aggBuffer[f] = val; break;
                case AggregationFunction.First: if (_aggCount == 0) _aggBuffer[f] = val; break;
                case AggregationFunction.Last: _aggBuffer[f] = val; break;
            }
        }
        _aggCount++;
    }

    private void EmitAggregatedBucket(Span<float> outputFrame)
    {
        for (int f = 0; f < InputDimension; f++)
        {
            float val = _aggBuffer[f];
            if (_aggFunc == AggregationFunction.Mean) val /= _aggCount;
            outputFrame[f] = val;
        }
    }

    private void ResetAggBuffer()
    {
        if (_aggFunc == AggregationFunction.Max) _aggBuffer.AsSpan().Fill(float.MinValue);
        else if (_aggFunc == AggregationFunction.Min) _aggBuffer.AsSpan().Fill(float.MaxValue);
        else _aggBuffer.AsSpan().Clear();
        _aggCount = 0;
    }

    private void Interpolate(ReadOnlySpan<float> y0, ReadOnlySpan<float> y1, float ratio, Span<float> output)
    {
        for (int f = 0; f < InputDimension; f++)
        {
            output[f] = y0[f] + (y1[f] - y0[f]) * ratio;
        }
    }
}