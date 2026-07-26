using Maldact.Backend.ML.ModelParameters;
using Maldact.Backend.ML.Modules;
using Maldact.Core.Data;
using Maldact.Core.ML.Training;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

namespace Maldact.Backend.ML.Training.Loops;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ML;

/// <summary>
/// Orchestrates tree-based training loops with memory-efficient streaming and stochastic multi-label resolution.
/// </summary>
public class TreeTrainingLoop : ITrainingLoop
{
    public enum TreeType { RandomForest, XgBoost }
    
    public required TreeType EnsembleType { get; init; }
    public required int NumberOfTrees { get; init; }
    public required int MaxDepth { get; init; }
    public required int NumClasses { get; init; }
    public int? Seed { get; init; } = null;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown if the data loader loads no data.</exception>>
    public async Task<IModelParameters> RunAsync(
        IFormattedLabeledTrainingDataLoader dataLoader, 
        IProgress<EpochMetrics>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            progress?.Report(new EpochMetrics(0, 1, 0, 0, 0, 0));

            var mlContext = new MLContext(seed: Seed);
            var rng = Seed.HasValue ? new Random(Seed.Value) : new Random();
            
            // dynamically resolve feature dimension from the first batch to satisfy ML.NET's strict fixed-vector schema requirements
            var firstBatch = dataLoader.GenerateBatches().FirstOrDefault();
            if (firstBatch == null) throw new InvalidOperationException("Data loader yielded no training batches.");
            
            int featureDimension = (int)firstBatch.FeatureShape[1];
            var schemaDef = SchemaDefinition.Create(typeof(TreeDataRow));
            schemaDef["Features"].ColumnType = new VectorDataViewType(NumberDataViewType.Single, featureDimension);

            var rawDataList = UnrollBatches(dataLoader.GenerateBatches(), NumClasses, rng).ToList();
            
            // guarantee ML.NETs internal dynamic mapping is flawless by injecting dummy rows for missing classes
            for (uint c = 0; c < NumClasses; c++)
            {
                if (rawDataList.All(r => r.Label != c))
                {
                    rawDataList.Add(new TreeDataRow 
                    { 
                        Features = new float[featureDimension], // blank features naturally evaluate to low confidence
                        Label = c 
                    });
                }
            }
            
            // inject strict schema definition during lazy evaluation
            var trainDataView = mlContext.Data.LoadFromEnumerable(rawDataList, schemaDef);
            
            var pipeline = mlContext.Transforms.Conversion.MapValueToKey("Label")
                .Append(BuildTreeTrainer(mlContext)) 
                .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            var trainedModel = pipeline.Fit(trainDataView);
    
            cancellationToken.ThrowIfCancellationRequested();

            var valDataView = mlContext.Data.LoadFromEnumerable(UnrollBatches(dataLoader.GetValidationBatches(), NumClasses, rng), schemaDef);
            var predictions = trainedModel.Transform(valDataView);
            var metrics = mlContext.MulticlassClassification.Evaluate(predictions);

            using var memoryStream = new MemoryStream();
            mlContext.Model.Save(trainedModel, trainDataView.Schema, memoryStream);

            progress?.Report(new EpochMetrics(1, 1, 0, metrics.LogLoss, 0, 0));

            return (IModelParameters)new TreeModelParameters(memoryStream.ToArray());
            
        }, cancellationToken);
    }
    
    /// <summary>
    /// Builds the training estimator for a given context
    /// </summary>
    private ITrainerEstimator<ISingleFeaturePredictionTransformer<object>, object> BuildTreeTrainer(MLContext mlContext)
    {
        int leaves = (int)Math.Pow(2, MaxDepth);

        return EnsembleType switch
        {
            TreeType.RandomForest => mlContext.MulticlassClassification.Trainers.OneVersusAll(
                mlContext.BinaryClassification.Trainers.FastForest(numberOfLeaves: leaves, numberOfTrees: NumberOfTrees)),
            
            TreeType.XgBoost => mlContext.MulticlassClassification.Trainers.OneVersusAll(
                mlContext.BinaryClassification.Trainers.LightGbm(numberOfLeaves: leaves, numberOfIterations: NumberOfTrees)),
            
            _ => throw new NotImplementedException($"Ensemble {EnsembleType} is not supported.")
        };
    }

    /// <summary>
    /// streams batch data into flattened rows. 
    /// if multiple labels are active, one is sampled at random to approximate multi-label distribution.
    /// </summary>
    private IEnumerable<TreeDataRow> UnrollBatches(IEnumerable<FormattedLabeledBatch> batches, int numClasses, Random rng)
    {
        int[] activeIndices = new int[numClasses];

        foreach (var batch in batches)
        {
            int batchSize = (int)batch.FeatureShape[0];
            int flatDim = (int)batch.FeatureShape[1];

            for (int b = 0; b < batchSize; b++)
            {
                float[] rowFeatures = new float[flatDim];
                Array.Copy(batch.FlattenedFeatures, b * flatDim, rowFeatures, 0, flatDim);

                int activeCount = 0;
                for (int c = 0; c < numClasses; c++)
                {
                    if (batch.FlattenedTargets[(b * numClasses) + c] > 0.5f)
                    {
                        activeIndices[activeCount++] = c;
                    }
                }

                uint rowLabel = activeCount > 0 
                    ? (uint)activeIndices[rng.Next(activeCount)] 
                    : 0;

                yield return new TreeDataRow { Features = rowFeatures, Label = rowLabel };
            }
        }
    }
}