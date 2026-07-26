namespace Maldact.Backend.ML.Modules;

using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

/// <summary>
/// A 1D Convolutional Neural Network designed for time-series feature extraction and sequence classification.
/// Expects pre-transposed input tensors aligned to spatial channels.
/// </summary>
public class TimeSeriesCnn : Module<Tensor, Tensor>
{
    private readonly Module<Tensor, Tensor> _layers;

    /// <summary>
    /// Initializes a new instance of the TimeSeriesCnn.
    /// </summary>
    /// <param name="name">The registered name of the module in the Torch computation graph.</param>
    /// <param name="inputChannels">The number of features (channels) in the input temporal frame.</param>
    /// <param name="channelSizes">An array defining the number of output filters for each sequential convolution layer.</param>
    /// <param name="kernelSize">The temporal receptive field size of the convolution kernels.</param>
    /// <param name="numClasses">The final number of classification targets.</param>
    public TimeSeriesCnn(
        string name, 
        int inputChannels, 
        int[] channelSizes, 
        int kernelSize, 
        int numClasses) : base(name)
    {
        ArgumentNullException.ThrowIfNull(channelSizes);
        if (channelSizes.Length == 0) throw new ArgumentException("Network must contain at least one hidden layer.", nameof(channelSizes));

        var sequentialList = new List<Module<Tensor, Tensor>>();
        int currentInputChannels = inputChannels;

        foreach (int outChannels in channelSizes)
        {
            sequentialList.Add(Conv1d(currentInputChannels, outChannels, kernelSize, padding: Padding.Same));
            sequentialList.Add(ReLU());
            currentInputChannels = outChannels;
        }

        // 1x1 conv collapses feature maps into class logits across the temporal dimension
        sequentialList.Add(Conv1d(currentInputChannels, numClasses, kernel_size: 1));

        _layers = Sequential(sequentialList.ToArray());

        // registers all sub-modules to the computational graph for autograd and parameter counting
        RegisterComponents();
    }

    /// <summary>
    /// Executes the forward pass of the network.
    /// </summary>
    /// <param name="input">The input tensor, expected in [Batch, Channel(Features), Sequence] format.</param>
    /// <returns>The predicted class logits, formatted as [Batch, Sequence, Classes].</returns>
    public override Tensor forward(Tensor input)
    {
        using var permutedInput = input.permute(0, 2, 1).contiguous();
        
        using var convOutput = _layers.forward(permutedInput);
        
        // transpose output from [N, Classes, L] to [N, L, Classes] to align with loss function targets
        return convOutput.permute(0, 2, 1).contiguous();
    }
}