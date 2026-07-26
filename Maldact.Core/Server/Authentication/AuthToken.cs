namespace Maldact.Core.Server.Authentication;

/// <summary>
/// Represents an immutable cryptographic token used for session authentication.
/// </summary>
/// <param name="Value">The underlying secure token string.</param>
public sealed record AuthToken(string Value)
{
    /// <inheritdoc />
    public override string ToString() => "[REDACTED_AUTH_TOKEN]";
}