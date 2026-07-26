using System.Text.Json;
using Maldact.Client.Networking;

namespace Maldact.Client.Indexing;

/// <summary>
/// Safely manages the persistent local storage of the active network connection state.
/// </summary>
public static class GlobalConnectionStateIndexManager
{
    // explicitly enforces case-insensitive mapping to prevent silent null mappings on custom DTOs
    private static readonly JsonSerializerOptions Options = new() 
    { 
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    
    private static string StateFilePath => Path.Combine(IndexingInvariantManager.GetConfigDirectory(), "connection.json");

    /// <summary>
    /// Reads the current connection routing state from disk.
    /// </summary>
    /// <returns>The deserialized connection state, or null if it does not exist.</returns>
    public static ConnectionStateIndex? GetCurrentState()
    {
        if (!File.Exists(StateFilePath)) return null;

        try
        {
            using var stream = new FileStream(StateFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return JsonSerializer.Deserialize<ConnectionStateIndex>(stream, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Overwrites the current network connection index state safely. Passing null purges the state.
    /// </summary>
    /// <param name="state">The new connection state to serialize.</param>
    public static void SetNewState(ConnectionStateIndex? state)
    {
        if (state == null)
        {
            Clear();
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(StateFilePath)!);

        using var stream = new FileStream(StateFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, state, Options);
    }

    /// <summary>
    /// Deletes the connection state file from disk.
    /// </summary>
    public static void Clear()
    {
        if (File.Exists(StateFilePath)) File.Delete(StateFilePath);
    }
}