using Maldact.Backend.ML.Inference;
using Maldact.Backend.ML.Inference.OverlapMerging;
using Maldact.Backend.ML.ModelParameters;
using Maldact.Backend.ML.Modules;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.ML.Inference;
using Maldact.Core.ML.Training;
using Microsoft.ML;
using Microsoft.ML.Data;
using TorchSharp;
using static TorchSharp.torch;

namespace Maldact.Backend.ML.Builders;

/// <summary>
/// Extension methods for securely constructing raw inference engines from model specifications and parameters.
/// </summary>
public static class RawInferenceBuilderExtensions
{
    /// <summary>
    /// Routes the model parameters and specifications to the correct underlying mathematical engine.
    /// </summary>
    /// <param name="parameters">The binary weights payload.</param>
    /// <param name="specification">The architectural boundaries.</param>
    /// <param name="preWarmedTreeEngine">Optional cached tree engine. Highly recommended for production to avoid ML.NET memory thrashing.</param>
    /// <returns>The fully initialized raw inference engine.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the model parameters are incompatible with the inference type.</exception>
    /// <exception cref="NotSupportedException">Thrown for unsupported overlap pooling methods.</exception>
    public static IRawInferenceEngine GetRawInference(
        this IModelParameters parameters,
        ModelSpecification specification,
        PredictionEngine<TreeDataRow, TreePrediction>? preWarmedTreeEngine = null)
    {
        if (specification.Algorithm is ModelSpecification.AlgorithmType.TreeEnsemble && preWarmedTreeEngine == null)
        {
            if (parameters is not TreeModelParameters treeParams)
                throw new InvalidOperationException("Parameters must be of type TreeModelParameters for TreeEnsemble algorithms.");

            var mlContext = new MLContext();
            using var stream = new MemoryStream(treeParams.ToBytes().ToArray());
            var cachedTreeModel = mlContext.Model.Load(stream, out _);
            
            int flatFeatureDimension = specification.WindowSize!.Value * specification.InputDimension!.Value;
            var schemaDef = SchemaDefinition.Create(typeof(TreeDataRow));
            schemaDef["Features"].ColumnType = new VectorDataViewType(NumberDataViewType.Single, flatFeatureDimension);
            
            preWarmedTreeEngine = mlContext.Model.CreatePredictionEngine<TreeDataRow, TreePrediction>(
                transformer: cachedTreeModel,
                inputSchemaDefinition: schemaDef
            );
        }
        
        return specification.Algorithm switch
        {
            ModelSpecification.AlgorithmType.Gru or ModelSpecification.AlgorithmType.Cnn =>
                parameters is TorchModelParameters torchParams 
                    ? specification.OverlapPoolingMethod switch
                    {
                        ModelSpecification.ResultOverlapTimePoolingMethod.Max 
                            => torchParams.GetRawTorchInference<MaxMerger>(specification),
                        
                        ModelSpecification.ResultOverlapTimePoolingMethod.Average 
                            => torchParams.GetRawTorchInference<AverageMerger>(specification),
                        
                        _ => throw new NotSupportedException($"Pooling method {specification.OverlapPoolingMethod} is not supported.")
                    }
                    : throw new InvalidOperationException("Parameters must be of type TorchModelParameters for deep learning algorithms."),

            ModelSpecification.AlgorithmType.TreeEnsemble => 
                (preWarmedTreeEngine ?? throw new InvalidOperationException("Tree model failed to initialize."))
                .GetRawTreeInference(specification),
            
            _ => throw new NotSupportedException($"Algorithm {specification.Algorithm} is not supported for inference.")
        };
    }

    /// <summary>
    /// Instantiates and loads the unmanaged memory state for a native TorchSharp neural network.
    /// </summary>
    /// <param name="parameters">The binary weights payload.</param>
    /// <param name="specification">The architectural boundaries.</param>
    /// <typeparam name="TMerger">The struct defining the window overlap merging method.</typeparam>
    /// <returns>The fully initialized raw inference engine.</returns>
    /// <exception cref="InvalidOperationException">Thrown for missing or incompatible module specifications.</exception>
    public static TorchInferenceEngine<TMerger> GetRawTorchInference<TMerger>(
        this TorchModelParameters parameters,
        ModelSpecification specification) where TMerger : struct, IOverlapMerger
    {
        var torchDevice = cuda.is_available() ? CUDA : CPU;
        var inputDimension = specification.InputDimension ?? 1;
        var outputDimension = specification.OutputDimension ?? 1;
        var windowSize = specification.WindowSize ?? 1;
        var windowStride = specification.WindowStride ?? 1;
        
        nn.Module<Tensor, Tensor> model = specification.Algorithm == ModelSpecification.AlgorithmType.Gru
            ? (specification.Gru ?? throw new InvalidOperationException("GRU specification missing.")).BuildModule("GruInference", inputDimension, outputDimension)
            : (specification.Cnn ?? throw new InvalidOperationException("CNN specification missing.")).BuildModule("CnnInference", inputDimension, outputDimension);
        
        using var stream = new MemoryStream(parameters.ToBytes().ToArray());
        model.load(stream);

        model.to(torchDevice);
        model.eval();

        return new TorchInferenceEngine<TMerger>(
            model: model,
            device: torchDevice,
            seqLength: windowSize,
            featureDim: inputDimension,
            stride: windowStride,
            numClasses: outputDimension
        );
    }

    /// <summary>
    /// Instantiates the sliding window wrapper for a classical ML.NET tree model.
    /// </summary>
    /// <param name="predictionEngine">Shared prediction engine.</param>
    /// <param name="specification">The required model specification.</param>
    /// <returns>The fully initialized raw tree inference engine.</returns>
    public static IRawInferenceEngine GetRawTreeInference(
        this PredictionEngine<TreeDataRow,TreePrediction> predictionEngine,
        ModelSpecification specification)
    {
        var inputDimension = specification.InputDimension ?? 1;
        var outputDimension = specification.OutputDimension ?? 1;
        var windowSize = specification.WindowSize ?? 1;
        var windowStride = specification.WindowStride ?? 1;

        return new TreeInferenceEngine(
            model: predictionEngine,
            seqLength: windowSize,
            featureDim: inputDimension,
            stride: windowStride,
            numClasses: outputDimension
        );
    }
}