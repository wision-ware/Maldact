using System.Buffers;
using System.Runtime.InteropServices;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.Data;
using Maldact.Core.ML.Consolidation.Tuning;
using Maldact.Core.ML.Inference;
using Maldact.Core.ML.Training;
using Maldact.Core.Results;

namespace Maldact.Backend.ML.Consolidation.Tuning;

/// <summary>
/// Default implementation of the iterative consolidator tuning loop.
/// Safely spools raw inference to disk via high-speed memory marshalling to prevent memory exhaustion.
/// </summary>
public class ConsolidatorTuningLoop : IConsolidatorTuningLoop
{
    private readonly IRawInferenceEngine _rawInference;
    private const float OverlapMatchPercentage = 0.5f;
    private readonly int _maxIterations;
    
    /// <summary>
    /// Initializes a new instance of the tuning loop.
    /// </summary>
    /// <param name="rawInference">The underlying ML engine used to generate raw prediction frames.</param>
    /// <param name="maxIterations">The hard limit on hyperparameter evaluation cycles.</param>
    public ConsolidatorTuningLoop(IRawInferenceEngine rawInference, int maxIterations)
    {
        _rawInference = rawInference;
        _maxIterations = maxIterations;
    }

    /// <inheritdoc />
    public async Task<ConsolidatorConfiguration> RunAsync(
        IRawContinuousLabeledDataLoader dataLoader, 
        IConsolidatorOptimizer optimizer,
        IProgress<TuningMetrics>? progress = null,
        CancellationToken token = default)
    {
        string tempFilePath = Path.GetTempFileName();

        try
        {
            return await Task.Run(() =>
            {
                // spool raw inferences to disk
                using (var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 131072))
                using (var writer = new BinaryWriter(fs))
                {
                    foreach (float[][] window in dataLoader.GetContinuousWindows())
                    {
                        token.ThrowIfCancellationRequested();
                        if (window.Length == 0) continue;

                        int featureDim = window[0].Length;

                        using (var chunk = new PipelineChunk(window, featureDim))
                        {
                            _rawInference.Classify(chunk);

                            ReadOnlySpan<float> probabilities = chunk.CurrentData;
                            writer.Write(probabilities.Length); 
                            
                            if (probabilities.Length > 0)
                            {
                                ReadOnlySpan<byte> byteSpan = MemoryMarshal.Cast<float, byte>(probabilities);
                                writer.Write(byteSpan); 
                            }
                        }
                    }
                }

                var groundTruth = dataLoader.GetGroundTruthEvents();
                
                float bestScore = -1f;
                ConsolidatorConfiguration? bestConfig = null;
                int iteration = 0;
                
                float? latestScore = null;
                var candidate = optimizer.SuggestNext(latestScore);
                
                // tuning loop
                while (candidate != null && iteration < _maxIterations)
                {
                    token.ThrowIfCancellationRequested();
                    var predictedEvents = new List<ResultEntry>();

                    // sequentially read binary blocks to feed the candidate consolidator
                    using (var fs = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 131072))
                    using (var reader = new BinaryReader(fs))
                    {
                        while (fs.Position < fs.Length)
                        {
                            int totalFloats = reader.ReadInt32();
                            if (totalFloats > 0)
                            {
                                int totalBytes = totalFloats * sizeof(float);
                                byte[] readBuffer = ArrayPool<byte>.Shared.Rent(totalBytes);
                                
                                int bytesRead = fs.Read(readBuffer, 0, totalBytes);
                                
                                using (var chunk = PipelineChunk.Rent(totalFloats, 1))
                                {
                                    chunk.LoadInferredStateFromBytes(readBuffer, 0, bytesRead);
                                    
                                    candidate.Consolidate(chunk);
                                    predictedEvents.AddRange(chunk.YieldFinal());
                                }
                                
                                ArrayPool<byte>.Shared.Return(readBuffer);
                            }
                        }
                    }

                    latestScore = CalculateF1Score(predictedEvents.ToArray(), groundTruth);

                    if (latestScore.Value > bestScore)
                    {
                        bestScore = latestScore.Value;
                        bestConfig = candidate.GetConsolidatorConfiguration();
                    }
                    
                    progress?.Report(new TuningMetrics(
                        iteration + 1,
                        _maxIterations,
                        bestConfig?.GetType().Name.Replace("Configuration", "") ?? "None",
                        bestScore,
                        latestScore.Value
                    ));
                    
                    candidate = optimizer.SuggestNext(latestScore);
                    iteration++;
                }

                if (bestConfig == null)
                    throw new InvalidOperationException("Optimizer yielded no candidates!");

                return bestConfig;

            }, token);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }
    
    /// <summary>
    /// Calculates the F1 score using an IoU matching strategy against ground truth events.
    /// </summary>
    private float CalculateF1Score(ResultEntry[] predicted, AbsoluteEvent[] groundTruth)
    {
        int truePositives = 0;
        int falsePositives = 0;

        var claimedTruths = new HashSet<string>(); 

        foreach (var pred in predicted)
        {
            AbsoluteEvent? bestMatch = null;
            float highestIoU = 0f;

            foreach (var gt in groundTruth)
            {
                if (gt.Class.ClassName != pred.Classification.ClassName) continue; 
                if (claimedTruths.Contains(gt.Id)) continue;

                float iou = CalculateIoU(pred, gt);
                if (iou >= OverlapMatchPercentage && iou > highestIoU)
                {
                    highestIoU = iou;
                    bestMatch = gt;
                }
            }

            if (bestMatch != null)
            {
                truePositives++;
                claimedTruths.Add(bestMatch.Id);
            }
            else
            {
                falsePositives++;
            }
        }

        int falseNegatives = groundTruth.Length - claimedTruths.Count;

        if (truePositives == 0) return 0f;
        
        float precision = (float)truePositives / (truePositives + falsePositives);
        float recall = (float)truePositives / (truePositives + falseNegatives);

        return 2 * (precision * recall) / (precision + recall);
    }

    /// <summary>
    /// Computes the temporal Intersection over Union (IoU) between a prediction and a ground truth event.
    /// </summary>
    private static float CalculateIoU(ResultEntry pred, AbsoluteEvent gt)
    {
        long startMax = Math.Max(pred.StartTime.AsRelative().Ticks, gt.StartOffset.Ticks);
        long endMin = Math.Min(pred.EndTime.AsRelative().Ticks, gt.EndOffset.Ticks);

        long overlap = Math.Max(0, endMin - startMax);
        if (overlap == 0) return 0f;

        long union = (pred.EndTime.AsRelative().Ticks - pred.StartTime.AsRelative().Ticks)
                     + (gt.EndOffset.Ticks - gt.StartOffset.Ticks)
                     - overlap;

        return (float)overlap / union;
    }
}