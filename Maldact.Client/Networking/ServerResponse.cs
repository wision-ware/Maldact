using System.Text;

namespace Maldact.Client.Networking;

/// <summary>
/// Defines the categorized resolution states of inbound server responses.
/// </summary>
public enum ResponseType
{
    /// <summary>
    /// The command executed successfully.
    /// </summary>
    Ok,
    
    /// <summary>
    /// The command encountered a fault or was rejected.
    /// </summary>
    Error,
    
    /// <summary>
    /// The server has allocated a stream and is awaiting a connection.
    /// </summary>
    StreamReady,
    
    /// <summary>
    /// The response contains an inference or database result.
    /// </summary>
    Result,
    
    /// <summary>
    /// The response contains server status or telemetry information.
    /// </summary>
    Status
}

/// <summary>
/// Represents a strongly-typed, parsed control protocol response from the server.
/// </summary>
/// <param name="Type">The categorized resolution state.</param>
/// <param name="Message">The primary summary line of the response.</param>
/// <param name="Payload">The optional multiline payload data.</param>
/// <param name="Port">The allocated streaming port, if applicable.</param>
/// <param name="ConnectionToken">The secure routing token for the stream, if applicable.</param>
public sealed record ServerResponse(
    ResponseType Type, 
    string Message,
    string? Payload,
    int? Port = null, 
    string? ConnectionToken = null)
{
    /// <summary>
    /// Parses a raw protocol string into a structured server response, utilizing a zero-allocation read loop for the payload.
    /// </summary>
    /// <param name="raw">The raw UTF-8 string read from the network stream.</param>
    /// <returns>A structured response record.</returns>
    /// <exception cref="ArgumentException">Thrown if the raw payload is empty or whitespace.</exception>
    public static ServerResponse Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ArgumentException("Cannot parse an empty server response.", nameof(raw));
        }

        using var reader = new StringReader(raw);
        
        var message = reader.ReadLine() ?? string.Empty;
        var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0)
        {
            return new ServerResponse(ResponseType.Status, message, null);
        }

        // drop the protocol framing colon if present
        var responseType = parts[0].TrimEnd(':');
        
        var payloadBuilder = new StringBuilder();
        bool isPayload = false;
        string? line;

        // stream through lines to avoid array allocations on massive stack traces or results
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            
            if (isPayload && trimmed == "[PAYLOAD END]") break;
            
            if (isPayload) 
            {
                payloadBuilder.AppendLine(line);
            }
            
            if (!isPayload && trimmed == "[PAYLOAD START]") 
            {
                isPayload = true;
            }
        }

        var payload = payloadBuilder.Length > 0 ? payloadBuilder.ToString().TrimEnd() : null;
        
        return responseType switch
        {
            "OK" => new ServerResponse(ResponseType.Ok, message, payload),
            "ERROR" => new ServerResponse(ResponseType.Error, message, payload),
            // safely guarded index access prevents crashing on malformed stream allocations
            "STREAM" when parts.Length >= 5 && int.TryParse(parts[2], out var port) 
                => new ServerResponse(ResponseType.StreamReady, message, payload, port, parts[4]),
            _ => new ServerResponse(ResponseType.Status, message, payload)
        };
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Payload) 
            ? Message 
            : $"{Message}{Environment.NewLine}{Payload}";
    }
}