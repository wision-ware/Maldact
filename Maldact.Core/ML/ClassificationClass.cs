namespace Maldact.Core.ML;

/// <summary>
/// Represents a strongly-typed categorization label for machine learning targets.
/// </summary>
public record ClassificationClass(string ClassName)
{
    public override string ToString() => ClassName;
}