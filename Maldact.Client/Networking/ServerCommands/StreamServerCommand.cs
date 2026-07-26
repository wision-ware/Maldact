namespace Maldact.Client.Networking.ServerCommands;

/// <summary>
/// Represents a control protocol command to request the allocation of a high-throughput telemetry stream.
/// </summary>
public sealed class StreamServerCommand() : ServerCommand("stream");