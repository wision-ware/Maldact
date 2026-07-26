using Maldact.Core.ML;
using Maldact.Core.Results;

namespace Maldact.Backend.ML.Consolidation;

/// <summary>
/// Defines a strict mathematical scoring contract for structural window consolidation engines.
/// </summary>
public interface IWindowScorer
{
    /// <summary>
    /// Calculates the updated state of the window based on incoming and exiting values.
    /// </summary>
    /// <param name="incomingValue">The new prediction value entering the window.</param>
    /// <param name="exitingValue">The oldest prediction value exiting the delay buffer.</param>
    /// <returns>The newly calculated mass.</returns>
    float CalculateScore(float incomingValue, float exitingValue);
}

/// <summary>
/// A high-performance, JIT-inlined generic engine that manages delayed-reader bounding boxes, 
/// zero-allocation flat ring buffers, and sub-frame continuous centroid math.
/// </summary>
/// <typeparam name="TScorer">The strict mathematical strategy struct used for window state evaluation.</typeparam>
public class SlidingWindowConsolidationEngine<TScorer> where TScorer : struct, IWindowScorer
{
    private readonly float _activationThreshold;
    private readonly float _deactivationThreshold;
    private readonly ClassificationClass[] _classes;
    private readonly int _windowSizeSamples;
    
    private long _totalSamplesProcessed;
    
    // flattened 1D array to guarantee contiguous CPU cache locality for the sliding window
    private readonly float[] _flatRingBuffer;
    private int _ringHead;

    // independently stateful struct arrays for zero-allocation strategy delegation
    private readonly TScorer[] _scorers;
    private readonly double[] _centroidAccumulators;
    private readonly double[] _centroidWeightTotals;
    private readonly long[] _beginIndices;
    private readonly bool[] _active;

    private readonly TimeStamper _stamper;
    private readonly List<ResultEntry> _yieldBuffer = [];

    /// <summary>
    /// initializes the core state machine and internal memory structures.
    /// </summary>
    public SlidingWindowConsolidationEngine(
        double sampleRateHz,
        StreamTime sessionStartTime,
        int windowSizeSamples,
        ClassificationClass[] classes,
        float activationThreshold,
        float deactivationThreshold,
        TScorer scorerTemplate)
    {
        _activationThreshold = activationThreshold;
        _deactivationThreshold = deactivationThreshold;
        _classes = classes;
        _windowSizeSamples = Math.Max(1, windowSizeSamples);
        
        _scorers = new TScorer[classes.Length];
        _beginIndices = new long[classes.Length];
        _active = new bool[classes.Length];
        _centroidAccumulators = new double[classes.Length];
        _centroidWeightTotals = new double[classes.Length];
        
        // strictly pre-allocate ring buffer bounds as a single flat array
        _flatRingBuffer = new float[_windowSizeSamples * classes.Length];

        for (int i = 0; i < classes.Length; i++)
        {
            // value-copy the template state to distribute configuration independently 
            _scorers[i] = scorerTemplate;
        }
        
        _stamper = new TimeStamper(sampleRateHz, sessionStartTime);
    }

    /// <summary>
    /// Executes the core hysteresis tracking loop over a multi-class prediction chunk via flat memory spans.
    /// </summary>
    public ResultEntry[] Consolidate(ReadOnlySpan<float> input)
    {
        if (input.Length == 0) return [];
        
        int numClasses = _classes.Length;
        if (input.Length % numClasses != 0) 
            throw new ArgumentException("Dimension of the vectors doesnt match number of supplied classes!");
        
        _yieldBuffer.Clear();
        int timeSteps = input.Length / numClasses;

        for (int t = 0; t < timeSteps; t++)
        {
            _totalSamplesProcessed++;
            int inputOffset = t * numClasses;
            int ringOffset = _ringHead * numClasses;
            long trailingIndex = Math.Max(0L, _totalSamplesProcessed - _windowSizeSamples);
            
            for (var j = 0; j < numClasses; j++)
            {   
                float incomingValue = input[inputOffset + j];
                float exitingValue = _flatRingBuffer[ringOffset + j];
                
                // overwrite the oldest value in the flat ring buffer with the new incoming value
                _flatRingBuffer[ringOffset + j] = incomingValue;
                
                // capture struct via ref to mutate array contents in-place without defensive copying
                ref var scorer = ref _scorers[j];
                var currentScore = scorer.CalculateScore(incomingValue, exitingValue);

                if (_active[j])
                {
                    _centroidAccumulators[j] += trailingIndex * exitingValue;
                    _centroidWeightTotals[j] += exitingValue;

                    if (currentScore > _deactivationThreshold) continue;
                    
                    var durationSamples = Math.Max(1L, trailingIndex - _beginIndices[j]);
                    var weight = _centroidWeightTotals[j];
                    
                    var centroidIndex = weight > 0 ? (_centroidAccumulators[j] / weight) : trailingIndex;
                    var meanScore = (float)(weight / durationSamples);
                    
                    _yieldBuffer.Add(
                        new ResultEntry(
                            _classes[j],
                            _stamper.GetTime(_beginIndices[j]),
                            _stamper.GetTime(trailingIndex),
                            _stamper.GetTime(centroidIndex),
                            meanScore,
                            Guid.NewGuid().ToString("N")
                        )
                    );
                        
                    _active[j] = false;
                    _centroidAccumulators[j] = 0.0;
                    _centroidWeightTotals[j] = 0.0;
                }
                else
                {
                    if (currentScore < _activationThreshold) continue;
                    
                    _active[j] = true;
                    _beginIndices[j] = trailingIndex;
                    
                    _centroidAccumulators[j] += trailingIndex * exitingValue;
                    _centroidWeightTotals[j] += exitingValue;
                }
            }
            
            _ringHead = (_ringHead + 1) % _windowSizeSamples;
        }

        return _yieldBuffer.Count > 0 ? _yieldBuffer.ToArray() : [];
    }
}