using System.Text;

namespace Maldact.Client.Networking.ServerCommands;

/// <summary>
/// Represents an outbound control protocol command to be executed by the server.
/// </summary>
public class ServerCommand
{
    private readonly string _commandText;

    /// <summary>
    /// Initializes a new instance of a client command.
    /// </summary>
    /// <param name="commandText">The raw CLI-formatted command string.</param>
    public ServerCommand(string commandText)
    {
        _commandText = commandText;
    }

    /// <summary>
    /// Encodes the command string into a UTF-8 byte array for network transmission.
    /// </summary>
    /// <returns>The encoded binary payload.</returns>
    public byte[] GetBytes() => Encoding.UTF8.GetBytes($"{_commandText}\n");
}