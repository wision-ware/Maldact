using Maldact.Backend.Server.Streaming;
using Maldact.Core.Data;
using Maldact.Core.ML;
using Maldact.Core.ML.Consolidation.Tuning;
using Maldact.Core.Results;

namespace Maldact.Backend.ML.Consolidation;

/// <summary>
/// Aggregates sequential frame predictions into events using a strict confidence threshold and a hang-frame duration.
/// Optimized for zero-allocation streaming via buffer reuse and flat memory spans.
/// </summary>
public class ThresholdAttentionResultConsolidator : ITunableResultConsolidator
{
    private readonly float _threshold;
    private readonly int _hangFrames;
    private readonly double _frameRateHz;
    private readonly ClassificationClass[] _classes;
    
    private long _totalFramesProcessed;
    
    // committed accumulations representing the official event bounds
    private readonly double[] _centroidAccumulators;
    private readonly double[] _centroidWeightTotals;
    
    // volatile accumulations capturing sub-threshold dips. 
    // committed if the event recovers, discarded if the event hangs and closes.
    private readonly double[] _pendingCentroidAccumulators;
    private readonly double[] _pendingWeightTotals;
    
    private readonly long[] _beginIndices;
    private readonly long[] _lastHitIndices;
    private readonly bool[] _active;

    private readonly TimeStamper _stamper;
    
    // reuse buffer to eliminate gen0 collection spikes during chunk streams
    private readonly List<ResultEntry> _yieldBuffer = new();

    /// <summary>
    /// Initializes a new instance of the threshold attention consolidator.
    /// </summary>
    /// <param name="frameRateHz">The sampling frequency of the incoming prediction frames.</param>
    /// <param name="sessionStartTime">The chronological start point of the data stream.</param>
    /// <param name="classes">The array of target classes mapped to the inference output indices.</param>
    /// <param name="threshold">The activation threshold to trigger an event.</param>
    /// <param name="hangFrames">The number of patience frames before dropping an event.</param>
    public ThresholdAttentionResultConsolidator(
        double frameRateHz, 
        StreamTime sessionStartTime, 
        ClassificationClass[] classes, 
        float threshold,
        int hangFrames)
    {
        _frameRateHz = frameRateHz;
        _classes = classes;
        _threshold = threshold;
        _hangFrames = hangFrames;
        
        _centroidAccumulators = new double[classes.Length];
        _centroidWeightTotals = new double[classes.Length];
        _pendingCentroidAccumulators = new double[classes.Length];
        _pendingWeightTotals = new double[classes.Length];
        
        _beginIndices = new long[classes.Length];
        _lastHitIndices = new long[classes.Length];
        _active = new bool[classes.Length];
        
        _stamper = new TimeStamper(frameRateHz, sessionStartTime);
    }

    /// <summary>
    /// Processes a sequential chunk of inference predictions via flat memory spans.
    /// Safely transitions the pipeline chunk to the Consolidated state and releases its unmanaged memory buffer.
    /// </summary>
    /// <param name="chunk">The zero-allocation data carrier in the Inferred state.</param>
    public void Consolidate(PipelineChunk chunk)
    {
        ReadOnlySpan<float> input = chunk.CurrentData;
        _yieldBuffer.Clear();

        if (input.Length > 0)
        {
            int numClasses = _classes.Length;
            int timeSteps = input.Length / numClasses;

            for (int t = 0; t < timeSteps; t++)
            {
                _totalFramesProcessed++;
                int offset = t * numClasses;

                for (var j = 0; j < numClasses; j++)
                {
                    float prob = input[offset + j];

                    if (prob >= _threshold)
                    {
                        if (!_active[j])
                        {
                            _active[j] = true;
                            _beginIndices[j] = _totalFramesProcessed;
                            
                            _centroidAccumulators[j] = 0;
                            _centroidWeightTotals[j] = 0;
                            _pendingCentroidAccumulators[j] = 0;
                            _pendingWeightTotals[j] = 0;
                        }
                        
                        _lastHitIndices[j] = _totalFramesProcessed; 
                        
                        // flush pending dip values (if any) to the main accumulator + add current frame
                        _centroidAccumulators[j] += _pendingCentroidAccumulators[j] + (_totalFramesProcessed * prob);
                        _centroidWeightTotals[j] += _pendingWeightTotals[j] + prob;
                        
                        // reset pending buffer as we are now stable above threshold
                        _pendingCentroidAccumulators[j] = 0;
                        _pendingWeightTotals[j] = 0;
                    }
                    else if (_active[j])
                    {
                        long framesSinceLastHit = _totalFramesProcessed - _lastHitIndices[j];
                        
                        if (framesSinceLastHit >= _hangFrames)
                        {
                            var eventDurationFrames = _lastHitIndices[j] - _beginIndices[j] + 1; 
                            var centroidIndex = _centroidAccumulators[j] / _centroidWeightTotals[j];
                            var meanScore = (float)(_centroidWeightTotals[j] / eventDurationFrames);

                            _yieldBuffer.Add(new ResultEntry(
                                _classes[j],
                                _stamper.GetTime(_beginIndices[j]),
                                _stamper.GetTime(_lastHitIndices[j]),
                                _stamper.GetTime((long)centroidIndex),
                                meanScore,
                                Guid.NewGuid().ToString("N") // N skips hyphen formatting overhead
                            ));

                            _active[j] = false;
                        }
                        else
                        {
                            // accumulate in volatile memory. 
                            // if we hit hangFrames, this is safely discarded. if we recover, it is committed.
                            _pendingCentroidAccumulators[j] += _totalFramesProcessed * prob;
                            _pendingWeightTotals[j] += prob;
                        }
                    }
                }
            }
        }

        // commit the results and safely return the float buffer to the ArrayPool
        chunk.TransitionToConsolidated(_yieldBuffer.Count > 0 ? _yieldBuffer.ToArray() : Array.Empty<ResultEntry>());
    }

    /// <inheritdoc />
    public ConsolidatorConfiguration GetConsolidatorConfiguration() => new BasicConfiguration
    {
        ClassNames = _classes.Select(c => c.ClassName).ToArray(),
        FrameRateHz = _frameRateHz,
        Threshold = _threshold,
        HangFrames = _hangFrames
    };
}