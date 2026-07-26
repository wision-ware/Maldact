namespace Maldact.Core.Server.Authentication;

/// <summary>
/// Defines the contract for an engine capable of resolving raw network tokens into verified state passes.
/// </summary>
public interface IAuthenticator
{
    /// <summary>
    /// Attempts to validate a network token and issue a corresponding server access pass.
    /// </summary>
    /// <param name="token">The raw token payload provided by the client.</param>
    /// <param name="pass">The issued access pass if authentication succeeds; otherwise, null.</param>
    /// <returns>True if the token is valid and a pass was issued; false otherwise.</returns>
    bool Authenticate(AuthToken token, out AccessPass? pass);
}