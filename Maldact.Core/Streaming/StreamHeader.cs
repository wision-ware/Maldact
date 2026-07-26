using System.Buffers.Binary;
using Maldact.Core.Results;

namespace Maldact.Core.Streaming;

/// <summary>
/// Represents the binary protocol header injected at the start of a transmission sequence to map connections to pending slots.
/// </summary>
public readonly struct StreamHeader
{
    private const int HeaderLength = 32;

    /// <summary>
    /// Gets the globally unique identifier matching the connection to the server's pending slot.
    /// </summary>
    public Guid Token { get; }
    
    /// <summary>
    /// Gets the absolute or relative zero-time index for the temporal stream.
    /// </summary>
    public StreamTime T0 { get; } 

    /// <summary>
    /// Initializes a new stream header payload.
    /// </summary>
    /// <param name="token">The routing token.</param>
    /// <param name="t0">The zero-time reference index.</param>
    public StreamHeader(Guid token, StreamTime t0)
    {
        Token = token;
        T0 = t0;
    }

    /// <summary>
    /// Initializes a stream header by deserializing a raw network byte sequence.
    /// </summary>
    /// <param name="bytes">The 32-byte memory span containing the header.</param>
    /// <exception cref="ArgumentException">Thrown if the provided span is smaller than the required 32-byte header length.</exception>
    /// <exception cref="InvalidOperationException">Thrown if an unknown temporal mode flag is encountered.</exception>
    public StreamHeader(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderLength)
            throw new ArgumentException($"Header must be at least {HeaderLength} bytes.");

        Token = new Guid(bytes[..16]);
        
        byte modeFlag = bytes[16];
        long ticks = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(17, 8));

        T0 = modeFlag switch
        {
            0 => new StreamTime(new DateTime(ticks, DateTimeKind.Utc)),
            1 => new StreamTime(new TimeSpan(ticks)),
            _ => throw new InvalidOperationException($"Unknown time mode flag: {modeFlag}")
        };
    }
    
    /// <summary>
    /// Serializes the header into a tightly packed 32-byte array for network transmission.
    /// </summary>
    /// <returns>The binary representation of the header.</returns>
    public byte[] ToBytes()
    {
        byte[] buffer = new byte[HeaderLength];
        
        Token.TryWriteBytes(buffer.AsSpan(0, 16));
        
        if (T0.IsAbsolute)
        {
            buffer[16] = 0;
            BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(17, 8), T0.AsAbsolute().Ticks);
        }
        else
        {
            buffer[16] = 1;
            BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(17, 8), T0.AsRelative().Ticks);
        }
        
        return buffer;
    }
}