using Maldact.Backend.ML.ModelParameters;
using Maldact.Backend.ML.Modules;
using Maldact.Core.Data;
using Maldact.Core.ML.Training;

namespace Maldact.Backend.ML.Training.Loops;

using TorchSharp;
using static TorchSharp.torch;

/// <summary>
/// Orchestrates the gradient descent training lifecycle for the TimeSeriesGru model.
/// Ensures safe native memory boundaries, responsive async cancellation, and LOH-optimized parameter serialization.
/// </summary>
public class GruTrainingLoop : ITrainingLoop
{
    private readonly TimeSeriesGru _model;
    
    /// <summary>
    /// Initializes a new instance of the training loop.
    /// </summary>
    /// <param name="model">The target recurrent neural network. Lifecycle disposal must be managed by the caller.</param>
    /// <exception cref="ArgumentNullException">Thrown if the provided model is null.</exception>
    public GruTrainingLoop(TimeSeriesGru model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }
    
    /// <summary>
    /// Gets the maximum number of epochs to train.
    /// </summary>
    public required int MaxEpochs { get; init; }

    /// <summary>
    /// Gets the learning rate for the Adam optimizer.
    /// </summary>
    public required double LearningRate { get; init; }

    /// <summary>
    /// Gets the consecutive epochs without validation improvement before early stopping triggers.
    /// </summary>
    public required int Patience { get; init; }

    /// <summary>
    /// Gets the target hardware device (CPU or CUDA) for tensor allocation.
    /// </summary>
    public required Device Device { get; init; }

    /// <summary>
    /// Gets the optional random seed for deterministic initialization.
    /// </summary>
    public int? Seed { get; init; }

    /// <inheritdoc />
    public async Task<IModelParameters> RunAsync(
        IFormattedLabeledTrainingDataLoader dataLoader, 
        IProgress<EpochMetrics>? progress = null, 
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            _model.to(Device);
                        
            if (Seed.HasValue)
            {
                random.manual_seed(Seed.Value);
                if (Device.type == DeviceType.CUDA)
                {
                    cuda.manual_seed(Seed.Value);
                    cuda.manual_seed_all(Seed.Value);
                }
            }
                
            using var criterion = nn.BCEWithLogitsLoss(); 
            using var optimizer = optim.Adam(_model.parameters(), lr: LearningRate);

            double bestValLoss = double.MaxValue;
            int currentPatience = 0;
            
            // allocate physical checkpoint file to dodge unmanaged stream closure traps
            string checkpointPath = Path.GetTempFileName();
            bool hasCheckpoint = false;

            try
            {
                for (int epoch = 0; epoch < MaxEpochs; epoch++)
                {
                    ct.ThrowIfCancellationRequested();

                    _model.train();
                    double trainLossSum = 0;
                    int trainBatches = 0;

                    foreach (var batch in dataLoader.GenerateBatches())
                    {
                        ct.ThrowIfCancellationRequested();

                        // trap implicit c++ allocations generated during recurrent unrolling
                        using var d = NewDisposeScope();

                        var x = tensor(batch.FlattenedFeatures, batch.FeatureShape).to(Device);
                        var y = tensor(batch.FlattenedTargets, batch.TargetShape).to(Device);

                        optimizer.zero_grad();
                        
                        var predictions = _model.forward(x);
                        var loss = criterion.forward(predictions, y);
                            
                        loss.backward();
                        optimizer.step();

                        trainLossSum += loss.ToSingle();
                        trainBatches++;
                    }

                    _model.eval();
                    double valLossSum = 0;
                    int valBatches = 0;

                    using (no_grad())
                    {
                        foreach (var batch in dataLoader.GetValidationBatches())
                        {
                            ct.ThrowIfCancellationRequested();
                            using var d = NewDisposeScope();

                            var x = tensor(batch.FlattenedFeatures, batch.FeatureShape).to(Device);
                            var y = tensor(batch.FlattenedTargets, batch.TargetShape).to(Device);

                            var predictions = _model.forward(x);
                            var loss = criterion.forward(predictions, y);

                            valLossSum += loss.ToSingle();
                            valBatches++;
                        }
                    }

                    double avgTrainLoss = trainBatches > 0 ? trainLossSum / trainBatches : 0;
                    double avgValLoss = valBatches > 0 ? valLossSum / valBatches : 0;

                    if (avgValLoss < bestValLoss)
                    {
                        bestValLoss = avgValLoss;
                        currentPatience = 0;

                        // overwrite physical file to prevent loh allocations on new best metrics
                        _model.save(checkpointPath);
                        hasCheckpoint = true;
                    }
                    else 
                    {
                        currentPatience++;
                    }
                    
                    progress?.Report(new EpochMetrics(
                        epoch + 1, 
                        MaxEpochs, 
                        avgTrainLoss, 
                        avgValLoss, 
                        currentPatience, 
                        Patience));
                    
                    if (currentPatience >= Patience) break;
                }

                if (!hasCheckpoint)
                {
                    _model.save(checkpointPath);
                }

                // load the optimal sequence weights into memory exactly once 
                byte[] finalWeights = File.ReadAllBytes(checkpointPath);
                return (IModelParameters)new TorchModelParameters(finalWeights);
            }
            finally
            {
                // strictly enforce cleanup of physical checkpoint data
                if (File.Exists(checkpointPath))
                {
                    File.Delete(checkpointPath);
                }
            }
        }, ct);
    }
}