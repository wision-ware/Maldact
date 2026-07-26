using Maldact.Core.ML;

namespace Maldact.Core.Results;

/// <summary>
/// Defines temporal and categorical boundaries used to filter inference results.
/// </summary>
public record ResultQuery
{ 
    /// <summary>
    /// Gets the set of target classes to filter by. If null, all classes are included.
    /// </summary>
    public IReadOnlySet<ClassificationClass>? Classes { get; init; }
    
    /// <summary>
    /// Gets the lower temporal bound of the query.
    /// </summary>
    public StreamTime? MinTime { get; init; }
    
    /// <summary>
    /// Gets the upper temporal bound of the query.
    /// </summary>
    public StreamTime? MaxTime { get; init; }

    /// <inheritdoc />
    public virtual bool Equals(ResultQuery? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        bool classesEqual = (Classes, other.Classes) switch
        {
            (null, null) => true,
            (not null, not null) => Classes.Count == other.Classes.Count && Classes.All(other.Classes.Contains),
            _ => false
        };

        return classesEqual && MinTime == other.MinTime && MaxTime == other.MaxTime;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(MinTime);
        hash.Add(MaxTime);
        
        if (Classes != null)
        {
            // order-independent hashing for sets
            int classHash = 0;
            foreach (var cls in Classes)
                classHash ^= cls.GetHashCode();
            hash.Add(classHash);
        }
        return hash.ToHashCode();
    }
}