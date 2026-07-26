namespace Maldact.Core.Config;

/// <summary>
/// Provides read-write access to a managed configuration state, allowing modifications to be flushed to persistent storage.
/// </summary>
/// <typeparam name="TConfig">The strongly-typed configuration schema.</typeparam>
public interface IConfigurationProvider<TConfig> : IReadonlyConfigurationProvider<TConfig> 
    where TConfig : class
{
    /// <summary>
    /// Overwrites the in-memory configuration state with a new instance.
    /// </summary>
    /// <param name="config">The mutated configuration state to apply.</param>
    void Update(TConfig config);

    /// <summary>
    /// Synchronizes the current in-memory configuration state with the underlying persistent storage (e.g., disk).
    /// </summary>
    void Flush();
}