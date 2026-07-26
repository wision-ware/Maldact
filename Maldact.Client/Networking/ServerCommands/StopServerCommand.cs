namespace Maldact.Client.Networking.ServerCommands;

/// <summary>
/// Issues an administrative command to gracefully terminate the server runtime.
/// </summary>
public sealed class StopServerCommand() : ServerCommand("stop");