using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace Maldact.Backend.Server.ControlProtocol.Responses;

/// <summary>
/// Represents a structured protocol response resulting from a command execution.
/// </summary>
/// <param name="ResponseType">The resolution classification of the command.</param>
/// <param name="Message">The primary message or status line of the output.</param>
/// <param name="Payload">The optional multiline data payload associated with the command.</param>
public sealed record CommandResponse(CommandResponse.Type ResponseType, string Message, object? Payload = null)
{
    /// <summary>
    /// Defines the classification states for a command response.
    /// </summary>
    public enum Type
    {
        /// <summary>
        /// The command executed successfully.
        /// </summary>
        Ok = 0,
        
        /// <summary>
        /// The command established a continuous stream.
        /// </summary>
        Stream = 1,
        
        /// <summary>
        /// The command encountered a critical fault.
        /// </summary>
        Error = 2
    }
    
    /// <summary>
    /// Serializes the response into the proprietary plain-text framing protocol.
    /// </summary>
    /// <returns>The serialized protocol payload.</returns>
    public override string ToString()
    {
        string identifier = ResponseType switch
        {
            Type.Ok => "OK",
            Type.Stream => "STREAM",
            Type.Error => "ERROR",
            _ => throw new InvalidOperationException($"Invalid response type mapping for: {ResponseType}")
        };

        var colon = string.IsNullOrWhiteSpace(Message) ? string.Empty : ": ";
        var nl = "\n";
        
        if (Payload is null)
        {
            return $"{identifier}{colon}{Message}{nl}";
        }

        return $"{identifier}{colon}{Message}{nl}[PAYLOAD START]{nl}{Payload}{nl}[PAYLOAD END]";
    }
}

/// <summary>
/// Provides high-performance extension methods for transmitting responses across network boundaries.
/// </summary>
public static class CommandResponseExtensions 
{
    /// <summary>
    /// Serializes and asynchronously writes the command response to the active network socket.
    /// </summary>
    /// <param name="stream">The active client network stream.</param>
    /// <param name="response">The response payload to transmit.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    public static async Task WriteAsync(this SslStream stream, CommandResponse response, CancellationToken cancellationToken)
    {
        // rent buffer or use standard memory allocation for the protocol string payload
        byte[] messageBytes = Encoding.UTF8.GetBytes(response.ToString());
        
        // strictly uses ReadOnlyMemory representation for optimized I/O pipeline dispatching
        await stream.WriteAsync(messageBytes.AsMemory(), cancellationToken);
    }
}