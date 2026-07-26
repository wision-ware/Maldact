using Maldact.Backend.Server.Authentication;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.Server.Authentication;

namespace Maldact.Backend.Server.Builders;

/// <summary>
/// Provides extension methods for safely constructing authentication engines from configuration payloads.
/// </summary>
public static class AuthenticatorBuilderExtensions
{
    /// <summary>
    /// Builds an in-memory authenticator using the keys defined in the server configuration.
    /// </summary>
    /// <param name="config">The validated server configuration payload.</param>
    /// <returns>An initialized authenticator instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the configuration payload is null.</exception>
    public static IAuthenticator BuildAuthenticator(this ServerConfiguration config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        // defensive projection against aggressive JSON deserializers stripping default initializers
        var users = config.UserKeys ?? Enumerable.Empty<string>();
        var admins = config.AdminKeys ?? Enumerable.Empty<string>();

        return new Authenticator(
            new HashSet<AuthToken>(users.Select(k => new AuthToken(k))), 
            new HashSet<AuthToken>(admins.Select(k => new AuthToken(k)))
        );
    }
}