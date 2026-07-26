namespace Maldact.Core.Config;

/// <summary>
/// Provides covariant, read-only access to a managed configuration state.
/// </summary>
/// <typeparam name="TConfig">The strongly-typed configuration schema.</typeparam>
public interface IReadonlyConfigurationProvider<out TConfig> 
    where TConfig : class
{
    /// <summary>
    /// Gets the current active configuration state.
    /// </summary>
    TConfig Config { get; }
}