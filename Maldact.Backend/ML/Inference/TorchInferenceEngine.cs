using System.Buffers;
using Maldact.Backend.ML.Inference.OverlapMerging;
using Maldact.Core.Data;
using Maldact.Core.ML;
using Maldact.Core.ML.Consolidation;
using Maldact.Core.ML.Inference;
using Maldact.Core.Results;
using TorchSharp;
using static TorchSharp.torch;

namespace Maldact.Backend.ML.Inference;

/// <summary>
/// A highly optimized sliding-window inference engine wrapping a TorchSharp neural network.
/// Implements pre-allocated contiguous memory buffers to eliminate Gen 0 thrashing and unmanaged tensor leaks.
/// </summary>
/// <typeparam name="TMerger">The struct-based overlap resolution strategy.</typeparam>
public class TorchInferenceEngine<TMerger> : IRawInferenceEngine
    where TMerger: struct, IOverlapMerger
{
    private readonly nn.Module<Tensor, Tensor> _model;
    private readonly Device _device;
    private readonly int _seqLength;
    private readonly int _featureDim;
    private readonly int _numClasses;
    private readonly int _stride;
    private readonly int _overlapSize;
    private readonly long[] _tensorShape;
    
    private float[] _rollingBuffer;
    private int _bufferedFrames;
    
    private TMerger _merger;
    
    private readonly float[] _flatDataBuffer;
    private readonly float[] _unresolvedTail;
    private bool _hasUnresolvedTail;
    
    /// <summary>
    /// Initializes a new instance of the Torch inference engine.
    /// </summary>
    /// <param name="model">The trained TorchSharp module.</param>
    /// <param name="device">The hardware device (CPU/CUDA) to execute the forward pass on.</param>
    /// <param name="seqLength">The required number of contiguous temporal frames per inference.</param>
    /// <param name="featureDim">The number of spatial features per frame.</param>
    /// <param name="stride">The number of frames to slide the window forward after each prediction.</param>
    /// <param name="numClasses">The final number of classification targets.</param>
    /// <exception cref="ArgumentNullException">Thrown if model or device are null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if any of the dimension parameters aren't positive.</exception>
    /// <exception cref="ArgumentException">Thrown if stride exceeds seqLength.</exception>
    public TorchInferenceEngine(
        nn.Module<Tensor, Tensor> model, 
        Device device,
        int seqLength, 
        int featureDim,
        int stride,
        int numClasses)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _merger = new TMerger();
        
        _seqLength = seqLength > 0 ? seqLength : throw new ArgumentOutOfRangeException(nameof(seqLength));
        _featureDim = featureDim > 0 ? featureDim : throw new ArgumentOutOfRangeException(nameof(featureDim));
        _stride = stride > 0 ? stride : throw new ArgumentOutOfRangeException(nameof(stride));
        _numClasses = numClasses > 0 ? numClasses : throw new ArgumentOutOfRangeException(nameof(numClasses));
        
        _overlapSize = seqLength - stride;
        if (_overlapSize < 0) throw new ArgumentException("Stride cannot exceed sequence length.");
        _tensorShape = [1, _seqLength, _featureDim];
        
        // init flat buffers with extra capacity to prevent early resizes
        _rollingBuffer = new float[_seqLength * _featureDim * 4]; 
        _flatDataBuffer = new float[_seqLength * _featureDim];
        _unresolvedTail = new float[_overlapSize * _numClasses];

        _model.eval();
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

        // dry-run output allocation
        int tempCount = _bufferedFrames;
        int totalOutputFrames = 0;
        while (tempCount >= _seqLength)
        {
            totalOutputFrames += _stride;
            tempCount -= _stride;
        }

        Span<float> outputBuffer = chunk.TransitionToInference(totalOutputFrames * _numClasses);
        int currentOutFrameOffset = 0;

        // hot path
        while (_bufferedFrames >= _seqLength)
        {
            // copy targeted sequence into feed buffer
            Array.Copy(_rollingBuffer, 0, _flatDataBuffer, 0, _seqLength * _featureDim);

            Span<float> currentWindowProbabilities = stackalloc float[_seqLength * _numClasses];

            // forward pass
            using (var scope = NewDisposeScope())
            {
                using var inputTensor = tensor(_flatDataBuffer, _tensorShape, device: _device);
                using var noGrad = no_grad();
                using var outputTensor = _model.forward(inputTensor);
                using var sigmoided = nn.functional.sigmoid(outputTensor);
    
                var tensorData = sigmoided.data<float>();
                tensorData.CopyTo(currentWindowProbabilities);
            }
            
            // resolve overlaps
            if (_hasUnresolvedTail)
            {
                for (int i = 0; i < _overlapSize; i++)
                {
                    Span<float> frameProbabilities = currentWindowProbabilities.Slice(i * _numClasses, _numClasses);
                    Span<float> tailProbabilities = _unresolvedTail.AsSpan(i * _numClasses, _numClasses);
                    _merger.Merge(frameProbabilities, tailProbabilities);
                }
            }
            
            // map resolved frames to output span
            for (int i = 0; i < _stride; i++)
            {
                Span<float> targetOutputFrame = outputBuffer.Slice((currentOutFrameOffset + i) * _numClasses, _numClasses);
                currentWindowProbabilities.Slice(i * _numClasses, _numClasses).CopyTo(targetOutputFrame);
            }
            currentOutFrameOffset += _stride;

            // cache suffix frames
            for (int i = 0; i < _overlapSize; i++)
            {
                Span<float> sourceSuffixFrame = currentWindowProbabilities.Slice((i + _stride) * _numClasses, _numClasses);
                Span<float> targetTailFrame = _unresolvedTail.AsSpan(i * _numClasses, _numClasses);
                sourceSuffixFrame.CopyTo(targetTailFrame);
            }
            
            _hasUnresolvedTail = true;
            
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
        _model.Dispose();
    }
}