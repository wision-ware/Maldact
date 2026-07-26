using Maldact.Core.ML.Consolidation.Tuning;

namespace Maldact.Backend.ML.Consolidation.Tuning.Optimizers;

/// <summary>
/// A composite strategy that multiplexes hyperparameter configurations across multiple optimizers sequentially.
/// </summary>
public class SequentialCompositeOptimizer : IConsolidatorOptimizer
{
    private readonly IConsolidatorOptimizer[] _optimizers;
    private readonly float?[] _scoreTracker;
    
    private int _currentIndex;
    private int _previousIndex;
    private bool _isExhausted;

    /// <summary>
    /// Initializes a new instance of the sequential composite optimizer.
    /// </summary>
    /// <param name="optimizers">The sequence of underlying optimizers to poll in a round-robin sequence.</param>
    /// <exception cref="ArgumentException">Thrown if the provided optimizer sequence is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if provided optimizer sequence is null</exception>
    public SequentialCompositeOptimizer(IList<IConsolidatorOptimizer> optimizers)
    {
        ArgumentNullException.ThrowIfNull(optimizers);
        
        // defensive copy to prevent external array mutations
        _optimizers = optimizers.ToArray();
        
        if (_optimizers.Length == 0)
            throw new ArgumentException("Composite optimizer requires at least one underlying optimizer.", nameof(optimizers));

        _scoreTracker = new float?[_optimizers.Length];
        
        // offset to the tail so the initial increment maps to index 0
        _previousIndex = _optimizers.Length - 1;
    }

    /// <inheritdoc />
    public ITunableResultConsolidator? SuggestNext(float? previousScore)
    {
        if (_isExhausted) return null;
        
        // route the latest evaluation score to the state of the optimizer that yielded it
        _scoreTracker[_previousIndex] = previousScore;
        
        ITunableResultConsolidator? candidate = null;
        int exhaustCounter = 0;
        
        while (candidate == null)
        {
            if (exhaustCounter >= _optimizers.Length)
            {
                _isExhausted = true;
                return null;
            }
            
            candidate = _optimizers[_currentIndex].SuggestNext(_scoreTracker[_currentIndex]);

            _previousIndex = _currentIndex;
            
            _currentIndex = (_currentIndex + 1) % _optimizers.Length;
            
            exhaustCounter++;
        }
        
        return candidate; 
    }
}