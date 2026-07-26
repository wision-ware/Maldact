namespace Maldact.Backend.ML.Modules;

using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

/// <summary>
/// A Gated Recurrent Unit (GRU) network tailored for time-series sequence classification.
/// Expects spatially aligned sequences where batches are the leading dimension.
/// </summary>
public class TimeSeriesGru : Module<Tensor, Tensor>
{
    private readonly GRU _gru;
    private readonly Module<Tensor, Tensor> _classifier;

    /// <summary>
    /// Initializes a new instance of the TimeSeriesGru.
    /// </summary>
    /// <param name="name">The registered name of the module in the Torch computation graph.</param>
    /// <param name="inputDimension">The number of features per temporal frame.</param>
    /// <param name="hiddenSize">The dimension of the recurrent hidden state.</param>
    /// <param name="numLayers">The number of stacked GRU layers.</param>
    /// <param name="numClasses">The final number of classification targets.</param>
    /// <param name="dropout">The dropout probability applied between stacked GRU layers.</param>
    public TimeSeriesGru(
        string name, 
        int inputDimension, 
        int hiddenSize, 
        int numLayers, 
        int numClasses, 
        double dropout) : base(name)
    {
        if (inputDimension <= 0) throw new ArgumentOutOfRangeException(nameof(inputDimension));
        if (hiddenSize <= 0) throw new ArgumentOutOfRangeException(nameof(hiddenSize));
        if (numClasses <= 0) throw new ArgumentOutOfRangeException(nameof(numClasses));

        _gru = GRU(
            inputSize: inputDimension, 
            hiddenSize: hiddenSize, 
            numLayers: numLayers, 
            batchFirst: true, // aligns with [Batch, Sequence, Features] formatting
            dropout: dropout);
        
        _classifier = Linear(hiddenSize, numClasses);

        // registers sub-modules for autograd parameter tracking
        RegisterComponents();
    }

    /// <summary>
    /// Executes the forward pass of the network.
    /// </summary>
    /// <param name="input">The input tensor, formatted as [Batch, Sequence, Features].</param>
    /// <returns>The predicted class logits, formatted as [Batch, Sequence, Classes].</returns>
    public override Tensor forward(Tensor input)
    {
        // the default c# overload handles the null hidden state zero-initialization automatically
        var (gruOutput, hiddenState) = _gru.forward(input); 

        // explicitly trap both unmanaged pointers to prevent c++ memory leaks
        using var _ = hiddenState;
        using var __ = gruOutput;

        // return un-disposed tensor to allow the loss function to trace the computation graph
        return _classifier.forward(gruOutput);
    }
}