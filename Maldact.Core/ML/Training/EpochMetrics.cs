namespace Maldact.Core.ML.Training;

/// <summary>
/// Encapsulates the performance data and state of a single training iteration.
/// </summary>
/// <param name="Epoch">The current 1-based epoch number.</param>
/// <param name="MaxEpochs">The total number of epochs scheduled for the training run.</param>
/// <param name="TrainLoss">The average loss computed over the training batches.</param>
/// <param name="ValLoss">The average loss computed over the validation batches.</param>
/// <param name="PatienceElapsed">The number of consecutive epochs without validation improvement.</param>
/// <param name="MaxPatience">The early stopping threshold.</param>
public sealed record EpochMetrics(
    int Epoch, 
    int MaxEpochs, 
    double TrainLoss, 
    double ValLoss, 
    int PatienceElapsed, 
    int MaxPatience
);