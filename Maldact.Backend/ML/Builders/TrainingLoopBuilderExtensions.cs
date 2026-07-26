using Maldact.Backend.ML.Training.Loops;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.ML.Training;
using Microsoft.ML.Trainers.FastTree;
using TorchSharp;

namespace Maldact.Backend.ML.Builders;

/// <summary>
/// Provides extension methods for securely instantiating the correct training loop orchestration engine.
/// </summary>
public static class TrainingLoopBuilderExtensions
{
    /// <summary>
    /// Builds the algorithmic training loop based on the provided model specification and hyperparameters.
    /// </summary>
    /// <param name="config">The training configuration defining the training parameters.</param>
    /// <param name="spec">Model specification defining the model topology and parameters.</param>
    /// <returns>The fully initialized training loop.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the configuration or specification is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if required algorithm-specific nested configurations are missing.</exception>
    /// <exception cref="NotSupportedException">For unsupported algorithms and unsupported tree ensemble types.</exception>
    public static ITrainingLoop BuildLoop(
        this TrainingConfiguration config,
        ModelSpecification spec)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (spec == null) throw new ArgumentNullException(nameof(spec));

        var torchDevice = config.Device == TrainingConfiguration.DeviceType.Cuda && torch.cuda.is_available() 
            ? torch.CUDA 
            : torch.CPU;
        
        if (config.RandomSeed.HasValue)
        {
            torch.random.manual_seed(config.RandomSeed.Value);
        
            if (config.Device == TrainingConfiguration.DeviceType.Cuda)
            {
                torch.cuda.manual_seed(config.RandomSeed.Value);
                torch.cuda.manual_seed_all(config.RandomSeed.Value);
            }
        }

        var name = spec.Name ?? "UnnamedModel";
        var epochs = config.MaxEpochs ?? 100;
        var lr = config.LearningRate ?? 0.001;
        var patience = config.Patience ?? 10;
        var inputDim = spec.InputDimension ?? 1;
        var numClasses = spec.OutputDimension ?? 1;
        var seed = config.RandomSeed;

        return spec.Algorithm switch
        {
            ModelSpecification.AlgorithmType.Gru => new GruTrainingLoop(
                (spec.Gru ?? throw new InvalidOperationException("GRU specification must be provided when the algorithm is Gru."))
                .BuildModule(name, inputDim, numClasses)
            )
            {
                MaxEpochs = epochs,
                LearningRate = lr,
                Patience = patience,
                Device = torchDevice,
                Seed = seed
            },

            ModelSpecification.AlgorithmType.Cnn => new CnnTrainingLoop(
                (spec.Cnn ?? throw new InvalidOperationException("CNN specification must be provided when the algorithm is Cnn."))
                .BuildModule(name, inputDim, numClasses)
            )
            {
                MaxEpochs = epochs,
                LearningRate = lr,
                Patience = patience,
                Device = torchDevice,
                Seed = seed
            },
        
            ModelSpecification.AlgorithmType.TreeEnsemble => new TreeTrainingLoop
            {
                EnsembleType = (spec.Tree ?? throw new InvalidOperationException("Tree specification must be provided when the algorithm is TreeEnsemble."))
                    .EnsembleType switch 
                {
                    TreeParameters.TreeType.RandomForest => TreeTrainingLoop.TreeType.RandomForest,
                    TreeParameters.TreeType.XgBoost => TreeTrainingLoop.TreeType.XgBoost,
                    null => throw new InvalidOperationException("Tree ensemble type must be explicitly specified."),
                    _ => throw new NotSupportedException($"Ensemble type {spec.Tree.EnsembleType} is not supported.")
                },
                NumberOfTrees = spec.Tree.NumberOfTrees,
                MaxDepth = spec.Tree.MaxDepth,
                NumClasses = numClasses,
            },

            _ => throw new NotSupportedException($"Algorithm {spec.Algorithm} is not supported for training loops.")
        };
    }
}