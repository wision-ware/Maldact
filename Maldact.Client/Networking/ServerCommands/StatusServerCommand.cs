namespace Maldact.Client.Networking.ServerCommands;

/// <summary>
/// Requests current diagnostic and telemetry metrics from the server.
/// </summary>
public sealed class StatusServerCommand() : ServerCommand("status");
