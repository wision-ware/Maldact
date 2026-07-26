using Google.Protobuf;
using Maldact.Backend.ML.Inference;
using Maldact.Backend.ML.Inference.OverlapMerging;
using Maldact.Backend.ML.ModelParameters;
using Maldact.Backend.ML.Modules;
using Maldact.Backend.ML.Packaging;
using Maldact.Backend.Preprocessing.Pipelines;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.ML.Consolidation;
using Maldact.Core.ML.Inference;
using Maldact.Core.Results;
using Microsoft.ML;
using Microsoft.ML.Data;
using TorchSharp;
using static TorchSharp.torch;

namespace Maldact.Backend.ML.Builders;

/// <summary>
/// A centralized factory responsible for parsing deployment artifacts and spawning stateful, 
/// stream-specific inference orchestrators.
/// </summary>
public class InferenceEngineFactory : IInferenceEngineFactory
{
    private readonly DeploymentArtifact _artifact;
    private readonly torch.Device _torchDevice;
    
    private readonly MLContext? _mlContext;
    private readonly ITransformer? _cachedTreeModel;

    /// <summary>
    /// Initializes the inference factory from a given deployment artifact.
    /// </summary>
    /// <param name="artifact">The deployment artifact to build from.</param>
    /// <exception cref="ArgumentNullException">Thrown if the artifact is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the artifact is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the artifact contains conflicting members.</exception>
    public InferenceEngineFactory(DeploymentArtifact artifact)
    {
        _artifact = artifact ?? throw new ArgumentNullException(nameof(artifact));
        
        var spec = _artifact.Specification ?? throw new ArgumentException("Artifact is missing the Model Specification.", nameof(artifact));
        var contract = _artifact.Contract ?? throw new ArgumentException("Artifact is missing the Data Contract.", nameof(artifact));
        
        _torchDevice = cuda.is_available() ? CUDA : CPU;

        // gracefully abort Torch/CNN initialization and load the ML.NET context
        if (spec.Algorithm == ModelSpecification.AlgorithmType.TreeEnsemble)
        {
            if (_artifact.Parameters is not TreeModelParameters treeParams)
                throw new InvalidOperationException($"Artifact parameters of type {_artifact.Parameters?.GetType().Name} do not match the TreeEnsemble algorithm.");

            var modelBytes = treeParams.ToBytes().ToArray();
            if (modelBytes.Length == 0)
                throw new InvalidOperationException("The TreeEnsemble model byte payload is empty.");

            _mlContext = new MLContext();
            using var stream = new MemoryStream(modelBytes);
            _cachedTreeModel = _mlContext.Model.Load(stream, out _);
        }
    }

    /// <inheritdoc />
    public IInferenceEngine Create(StreamTime sessionStartTime)
    {
        IRawInferenceEngine rawInferenceEngine;
        
        if (_artifact.Specification.Algorithm == ModelSpecification.AlgorithmType.TreeEnsemble)
        {
            int flatFeatureDimension = _artifact.Specification.WindowSize!.Value * _artifact.Specification.InputDimension!.Value;
            
            var schemaDef = SchemaDefinition.Create(typeof(TreeDataRow));
            schemaDef["Features"].ColumnType = new VectorDataViewType(NumberDataViewType.Single, flatFeatureDimension);
            
            var predictionEngine = _mlContext != null ? _mlContext.Model.CreatePredictionEngine<TreeDataRow, TreePrediction>(
                transformer: _cachedTreeModel,
                inputSchemaDefinition: schemaDef
            ) : throw new InvalidOperationException(
                "Tree model was not initialized correctly during factory construction.");

            rawInferenceEngine = _cachedTreeModel != null
                ? predictionEngine.GetRawTreeInference(_artifact.Specification)
                : throw new InvalidOperationException(
                    "Tree model was not initialized correctly during factory construction.");
        }
        else
        {
            rawInferenceEngine = _artifact.Specification.Algorithm switch
            {
                ModelSpecification.AlgorithmType.Gru or ModelSpecification.AlgorithmType.Cnn =>
                    _artifact.Parameters is TorchModelParameters torchParams 
                        ? _artifact.Specification.OverlapPoolingMethod switch
                        {
                            ModelSpecification.ResultOverlapTimePoolingMethod.Max 
                                => torchParams.GetRawTorchInference<MaxMerger>(_artifact.Specification),
                        
                            ModelSpecification.ResultOverlapTimePoolingMethod.Average 
                                => torchParams.GetRawTorchInference<AverageMerger>(_artifact.Specification),
                        
                            _ => throw new NotSupportedException($"Pooling method {_artifact.Specification.OverlapPoolingMethod} is not supported.")
                        }
                        : throw new InvalidOperationException("Artifact parameters do not match Torch algorithm types."),
                
                _ => throw new NotSupportedException($"Algorithm {_artifact.Specification.Algorithm} is not supported for inference.")
            };
        }
        
        var pipeline = _artifact.Contract.BuildPipeline();
        var consolidator = _artifact.ConsolidatorConfiguration.BuildConsolidator(sessionStartTime);
        
        return new InferenceEngine(rawInferenceEngine, consolidator, pipeline);
    }
}