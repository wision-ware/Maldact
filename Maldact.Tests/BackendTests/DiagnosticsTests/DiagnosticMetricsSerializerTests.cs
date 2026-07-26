using System.Text.Json;
using FluentAssertions;
using Maldact.Backend.Diagnostics;
using Maldact.Backend.Server.Streaming;

namespace Maldact.Tests.BackendTests.DiagnosticsTests;

/// <summary>
/// Verifies the compile-time JSON source generation, string enum conversion, and camelCase naming policies for telemetry payloads.
/// </summary>
public class DiagnosticMetricsSerializerTests
{
    
    [Fact]
    public void Roundtrip_StringSerialization_MaintainsExactStructuralEquality()
    {
        // arrange
        var original = CreateSampleMetrics();

        // act
        var jsonString = DiagnosticMetricsSerializer.SerializeToString(original);
        var deserialized = DiagnosticMetricsSerializer.DeserializeFromString(jsonString);

        // assert
        deserialized.Should().NotBeNull();
        deserialized.Should().Be(original, "the deserialized object graph must strictly match the original in sequence and values.");
    }

   
    [Fact]
    public void Roundtrip_Utf8BytesSerialization_MaintainsExactStructuralEquality()
    {
        // arrange
        var original = CreateSampleMetrics();

        // act
        var utf8Bytes = DiagnosticMetricsSerializer.SerializeToUtf8Bytes(original);
        var deserialized = DiagnosticMetricsSerializer.DeserializeFromUtf8Bytes(utf8Bytes);

        // assert
        utf8Bytes.Length.Should().BeGreaterThan(0);
        deserialized.Should().NotBeNull();
        deserialized.Should().Be(original);
    }

    
    [Fact]
    public void Serialize_PropertyNaming_AppliesCamelCasePolicy()
    {
        // arrange
        var metrics = CreateSampleMetrics();

        // act
        var jsonString = DiagnosticMetricsSerializer.SerializeToString(metrics);

        // assert
        jsonString.Should().Contain("\"serverStartTime\":");
        jsonString.Should().Contain("\"activeStreamingSessions\":");
        jsonString.Should().Contain("\"sessionPassId\":");
        jsonString.Should().NotContain("\"ServerStartTime\":", "PascalCase properties must be down-cased per the generator policy.");
    }

    
    [Fact]
    public void Serialize_EnumValues_AreWrittenAsStrings()
    {
        // arrange
        var metrics = CreateSampleMetrics();

        // act
        var jsonString = DiagnosticMetricsSerializer.SerializeToString(metrics);

        // assert
        jsonString.Should().Contain("\"Active\"");
        jsonString.Should().NotContain($"\"state\":{(int)SlotState.Active}", "enums must be written as their string representation.");
    }

    
    [Fact]
    public void Deserialize_MalformedJsonString_ThrowsJsonException()
    {
        // arrange
        const string malformedJson = "{ \"serverStartTime\": \"bad-date\", \"activeStreamingSessions\": \"not-a-number\" ]";

        // act & assert
        FluentActions.Invoking(() => DiagnosticMetricsSerializer.DeserializeFromString(malformedJson))
            .Should().Throw<JsonException>();
    }

    
    [Fact]
    public void Deserialize_MalformedUtf8Bytes_ThrowsJsonException()
    {
        // arrange
        var malformedBytes = System.Text.Encoding.UTF8.GetBytes("{ \"broken\": true ]");

        // act & assert
        FluentActions.Invoking(() => DiagnosticMetricsSerializer.DeserializeFromUtf8Bytes(malformedBytes))
            .Should().Throw<JsonException>();
    }

   
    private static ServerDiagnosticMetrics CreateSampleMetrics()
    {
        var session1 = new StreamDiagnostic(
            SessionPassId: "admin-token-1",
            RemoteEndpoint: "192.168.1.100",
            State: SlotState.Active,
            CachedResults: 5000,
            Uptime: TimeSpan.FromHours(2)
        );

        var session2 = new StreamDiagnostic(
            SessionPassId: "user-token-1",
            RemoteEndpoint: "10.0.0.5",
            State: SlotState.Reserved,
            CachedResults: 0,
            Uptime: TimeSpan.Zero
        );

        return new ServerDiagnosticMetrics(
            ServerStartTime: DateTimeOffset.UtcNow.AddDays(-1),
            ActiveStreamingSessions: 2,
            HangingRepositories: 0,
            TotalCachedResults: 5000,
            EstimatedMemoryUsageBytes: 1024 * 1024 * 50, // 50MB
            Sessions: new[] { session1, session2 }
        );
    }
}