using Maldact.Core.Server.Authentication;

namespace Maldact.Client.Indexing;

/// <summary>
/// Represents the immutable routing configuration and identity state required to establish a secure server session.
/// </summary>
public sealed record ConnectionStateIndex
{
    /// <summary>
    /// Initializes the target server hostname or IP address.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// Initializes the target server control port.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Initializes the cryptographic token used for session authentication.
    /// </summary>
    public required AuthToken AuthToken { get; init; }
}