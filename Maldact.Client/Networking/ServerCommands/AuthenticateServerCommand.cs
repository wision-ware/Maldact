using Maldact.Core.Server.Authentication;

namespace Maldact.Client.Networking.ServerCommands;

/// <summary>
/// Represents a control protocol command to authenticate the client session securely.
/// </summary>
/// <param name="authToken">The cryptographic token to validate.</param>
public sealed class AuthenticateServerCommand(AuthToken authToken) 
    : ServerCommand($"authenticate {authToken.Value}");
