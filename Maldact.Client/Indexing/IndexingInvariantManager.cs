namespace Maldact.Client.Indexing;

/// <summary>
/// Resolves OS-specific directory invariants for local configuration storage, supporting safe test isolation.
/// </summary>
public static class IndexingInvariantManager
{
    private static string? _overrideDirectory = null;

    /// <summary>
    /// Retrieves the standard configuration directory path for the current operating system, or a test override if specified.
    /// </summary>
    /// <returns>The absolute path to the configuration directory.</returns>
    public static string GetConfigDirectory()
    {
        if (!string.IsNullOrEmpty(_overrideDirectory))
        {
            return _overrideDirectory;
        }

        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Maldact");
        }
        
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".maldact");
    }
    
    internal static void OverrideSandboxPath(string? path)
    {
        _overrideDirectory = path; 
    }
}