using Maldact.Core.Server.Authentication;

namespace Maldact.Backend.Server.Authentication;

/// <summary>
/// Provides immutable, in-memory implementation of the token authenticator.
/// </summary>
public class Authenticator : IAuthenticator
{
    private readonly Dictionary<AuthToken, AccessPass> _passes;
    
    /// <summary>
    /// Initializes the authenticator with predefined sets of authorized tokens securely.
    /// </summary>
    /// <param name="userTokens">The collection of tokens granted standard user access.</param>
    /// <param name="adminTokens">The collection of tokens granted elevated administrative access.</param>
    /// <exception cref="ArgumentNullException">Thrown if either token set is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if a token is assigned to multiple overlapping roles.</exception>
    public Authenticator(IReadOnlySet<AuthToken> userTokens, IReadOnlySet<AuthToken> adminTokens)
    {
        ArgumentNullException.ThrowIfNull(userTokens);
        ArgumentNullException.ThrowIfNull(adminTokens);

        // pre-allocate capacity to eliminate resizing overhead
        int capacity = userTokens.Count + adminTokens.Count;
        _passes = new Dictionary<AuthToken, AccessPass>(capacity);

        foreach (var token in userTokens)
        {
            _passes.Add(token, new AccessPass(AccessPass.Role.User));
        }

        foreach (var token in adminTokens)
        {
            // explicitly catch security misconfigurations
            if (!_passes.TryAdd(token, new AccessPass(AccessPass.Role.Admin)))
            {
                throw new InvalidOperationException(
                    $"Security Configuration Error: Token '{token}' is ambiguously mapped to multiple roles.");
            }
        }
    }

    /// <inheritdoc />
    public bool Authenticate(AuthToken token, out AccessPass? pass)
    {
        return _passes.TryGetValue(token, out pass);
    }
}