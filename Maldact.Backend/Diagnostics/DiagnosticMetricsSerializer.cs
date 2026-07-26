using System.Text.Json;
using System.Text.Json.Serialization;

namespace Maldact.Backend.Diagnostics;

/// <summary>
/// Provides high-performance, compile-time generated serialization for server diagnostic payloads.
/// </summary>
public static class DiagnosticMetricsSerializer
{
    /// <summary>
    /// Serializes the diagnostic metrics into a UTF-8 encoded byte array using compile-time type resolution.
    /// </summary>
    /// <param name="metrics">The telemetry snapshot to serialize.</param>
    /// <returns>A compact, UTF-8 encoded binary representation of the JSON payload.</returns>
    public static byte[] SerializeToUtf8Bytes(ServerDiagnosticMetrics metrics)
    {
        return JsonSerializer.SerializeToUtf8Bytes(metrics, DiagnosticJsonContext.Default.ServerDiagnosticMetrics);
    }
    
    /// <summary>
    /// Deserializes a UTF-8 encoded byte span back into a diagnostic metric snapshot.
    /// </summary>
    /// <param name="utf8Bytes">The raw memory span containing the UTF-8 JSON payload.</param>
    /// <returns>The deserialized diagnostic metrics.</returns>
    /// <exception cref="InvalidDataException">Thrown if the deserialized payload resolves to null.</exception>
    /// <exception cref="JsonException">Thrown if the JSON structure is malformed or invalid.</exception>
    public static ServerDiagnosticMetrics DeserializeFromUtf8Bytes(ReadOnlySpan<byte> utf8Bytes)
    {
        return JsonSerializer.Deserialize(utf8Bytes, DiagnosticJsonContext.Default.ServerDiagnosticMetrics)
               ?? throw new InvalidDataException("Failed to deserialize diagnostic metrics from the server payload.");
    }
    
    /// <summary>
    /// Serializes the diagnostic metrics into a standard UTF-16 JSON string.
    /// </summary>
    /// <param name="metrics">The telemetry snapshot to serialize.</param>
    /// <returns>The JSON string representation.</returns>
    public static string SerializeToString(ServerDiagnosticMetrics metrics)
    {
        return JsonSerializer.Serialize(metrics, DiagnosticJsonContext.Default.ServerDiagnosticMetrics);
    }
    
    /// <summary>
    /// Deserializes a standard UTF-16 JSON string back into a diagnostic metric snapshot.
    /// </summary>
    /// <param name="jsonString">The raw JSON string payload.</param>
    /// <returns>The deserialized diagnostic metrics.</returns>
    /// <exception cref="InvalidDataException">Thrown if the deserialized payload resolves to null.</exception>
    /// <exception cref="JsonException">Thrown if the JSON structure is malformed or invalid.</exception>
    public static ServerDiagnosticMetrics DeserializeFromString(string jsonString)
    {
        return JsonSerializer.Deserialize(jsonString, DiagnosticJsonContext.Default.ServerDiagnosticMetrics)
               ?? throw new InvalidDataException("Failed to deserialize diagnostic metrics from the provided JSON string.");
    }
}

/// <summary>
/// A compile-time source generator context that maps the diagnostic records into highly optimized serialization code.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ServerDiagnosticMetrics))]
internal partial class DiagnosticJsonContext : JsonSerializerContext
{
    // the .NET compiler automatically injects the optimized serialization instructions here
}