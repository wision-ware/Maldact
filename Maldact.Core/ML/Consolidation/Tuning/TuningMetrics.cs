namespace Maldact.Core.ML.Consolidation.Tuning;

/// <summary>
/// Carries telemetry data representing the current state of a hyperparameter optimization sweep.
/// Structured as a readonly record struct to completely eliminate Gen 0 allocations during high-frequency progress reporting.
/// </summary>
/// <param name="CurrentIteration">The number of elapsed iterations in the tuning process</param>
/// <param name="MaxIterations">The maximum iteration budget set at startup</param>
/// <param name="BestAlgorithm">The current best algorithm</param>
/// <param name="BestScore">The current best f1 score</param>
/// <param name="LatestScore">The latest evaluated score</param>
public readonly record struct TuningMetrics(
    int CurrentIteration, 
    int MaxIterations, 
    string BestAlgorithm, 
    float BestScore, 
    float LatestScore);