using System.Buffers;
using Maldact.Backend.ML.Inference.OverlapMerging;
using Maldact.Backend.ML.Modules;
using Maldact.Core.Data;
using Maldact.Core.ML;
using Maldact.Core.ML.Consolidation;
using Maldact.Core.ML.Inference;
using Maldact.Core.Results;
using Microsoft.ML;

namespace Maldact.Backend.ML.Inference;

/// <summary>
/// A zero-allocation sliding-window inference engine that flattens time-series frames into classical ML.NET tree algorithms.
/// Operates directly on a contiguous flat shift-buffer to avoid heap thrashing and ArrayPool bottlenecks.
/// </summary>
public class TreeInferenceEngine : IRawInferenceEngine
{
    private readonly PredictionEngine<TreeDataRow, TreePrediction> _model;
    private readonly int _featureDim;
    private readonly int _seqLength;
    private readonly int _stride;
    private readonly int _numClasses;
    
    private float[] _rollingBuffer;
    private int _bufferedFrames;
    
    // pre-allocated structures to completely eliminate gc thrashing inside the streaming loop
    private readonly float[] _flatWindowBuffer;
    private readonly TreeDataRow _reusableDataRow;
    private TreePrediction _reusablePrediction;
    
    /// <summary>
    /// Initializes a new instance of the ML.NET tree inference engine.
    /// </summary>
    /// <param name="model">The ML.NET prediction engine loaded with the trained forest/tree weights.</param>
    /// <param name="seqLength">The required number of contiguous temporal frames per inference.</param>
    /// <param name="featureDim">The number of spatial features per frame.</param>
    /// <param name="stride">The number of frames to slide the window forward after each prediction.</param>
    /// <param name="numClasses">The total number of classification target outputs.</param>
    /// <exception cref="ArgumentNullException">Thrown if the model is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for negative dimension parameters.</exception>
    public TreeInferenceEngine(
        PredictionEngine<TreeDataRow, TreePrediction> model,
        int seqLength, 
        int featureDim,
        int stride,
        int numClasses)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _seqLength = seqLength > 0 ? seqLength : throw new ArgumentOutOfRangeException(nameof(seqLength));
        _featureDim = featureDim > 0 ? featureDim : throw new ArgumentOutOfRangeException(nameof(featureDim));
        _stride = stride > 0 ? stride : throw new ArgumentOutOfRangeException(nameof(stride));
        _numClasses = numClasses > 0 ? numClasses : throw new ArgumentOutOfRangeException(nameof(numClasses));

        // init flat buffers with extra capacity to prevent early resizes
        _rollingBuffer = new float[_seqLength * _featureDim * 4];
        _flatWindowBuffer = new float[_seqLength * _featureDim];
        
        // ml.net allows in-place mutation of the feature array, skipping object allocations
        _reusableDataRow = new TreeDataRow 
        { 
            Features = _flatWindowBuffer, 
            Label = 0 
        };
        
        _reusablePrediction = new TreePrediction 
        {
            Probabilities = new float[numClasses] 
        };
    }

    /// <inheritdoc />
    public void Classify(PipelineChunk chunk)
    {
        if (chunk == null) throw new ArgumentNullException(nameof(chunk));
        ReadOnlySpan<float> inputData = chunk.CurrentData;
        if (inputData.Length == 0) return;

        int incomingFrames = inputData.Length / _featureDim;
        
        // append incoming flat data directly to the tail
        EnsureBufferCapacity(_bufferedFrames + incomingFrames);
        inputData.CopyTo(_rollingBuffer.AsSpan(_bufferedFrames * _featureDim));
        _bufferedFrames += incomingFrames;

        // dry-run output allocation capacity
        int tempCount = _bufferedFrames;
        int windowCount = 0;
        while (tempCount >= _seqLength)
        {
            windowCount++;
            tempCount -= _stride;
        }

        Span<float> outputBuffer = chunk.TransitionToInference(windowCount * _numClasses);
        int currentOutOffset = 0;
        
        // hot path
        while (_bufferedFrames >= _seqLength)
        {
            // copy targeted sequence into ml.net feed buffer
            Array.Copy(_rollingBuffer, 0, _flatWindowBuffer, 0, _seqLength * _featureDim);
            
            // execute forward pass using the mutated reference structure
            _model.Predict(_reusableDataRow, ref _reusablePrediction);
            
            // copy probabilities directly into the flat chunk buffer span
            Span<float> targetOutputFrame = outputBuffer.Slice(currentOutOffset * _numClasses, _numClasses);
            _reusablePrediction.Probabilities.AsSpan().CopyTo(targetOutputFrame);
            currentOutOffset++;
            
            // hardware-accelerated left shift
            int framesRemaining = _bufferedFrames - _stride;
            if (framesRemaining > 0)
            {
                Array.Copy(
                    sourceArray: _rollingBuffer, 
                    sourceIndex: _stride * _featureDim, 
                    destinationArray: _rollingBuffer, 
                    destinationIndex: 0, 
                    length: framesRemaining * _featureDim);
            }
            _bufferedFrames = framesRemaining;
        }
    }

    // dynamically resizes buffer for massive chunks
    private void EnsureBufferCapacity(int requiredFrames)
    {
        int requiredFloats = requiredFrames * _featureDim;
        if (requiredFloats > _rollingBuffer.Length)
        {
            int newCapacity = Math.Max(_rollingBuffer.Length * 2, requiredFloats);
            var newBuffer = new float[newCapacity];
            Array.Copy(_rollingBuffer, 0, newBuffer, 0, _bufferedFrames * _featureDim);
            _rollingBuffer = newBuffer;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // rely on gc for cleanup, memory is unmanaged after dereference
    }
}