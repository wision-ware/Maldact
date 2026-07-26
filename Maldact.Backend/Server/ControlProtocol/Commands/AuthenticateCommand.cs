using System.CommandLine;
using System.Linq.Expressions;
using Maldact.Core.Server;
using Maldact.Core.Server.Authentication;

namespace Maldact.Backend.Server.ControlProtocol.Commands;

/// <summary>
/// Handles session authentication via identity tokens.
/// </summary>
internal class AuthenticateCommand : CommandWrapper
{
    /// <summary>
    /// Gets the root command node for authentication.
    /// </summary>
    public sealed override Command Command { get; protected set; }
    
    /// <summary>
    /// Initializes a new instance of the authentication command.
    /// </summary>
    /// <param name="context">The execution context for the current session.</param>
    public AuthenticateCommand(SessionContext context) : base(context)
    {
        Command = new Command("authenticate", "Authenticate for regular or admin access.")
        {
            new Argument<string>("auth-token")
            {
                Description = "The identity token to authenticate with."
            }
        };
        Command.SetAction(AuthenticateHandler);
    }

    private async Task AuthenticateHandler(ParseResult parseResult)
    {
        var tokenValue = parseResult.GetValue<string>("auth-token");
        
        if (string.IsNullOrWhiteSpace(tokenValue))
        {
            await parseResult.Configuration.Error.WriteLineAsync("No auth token provided.");
            return;
        }
        
        var token = new AuthToken(tokenValue);
        
        if (Context.Authenticator.Authenticate(token, out var pass) && pass != null)
        {
            Context.SetAuthenticatedState(pass);
            
            await parseResult.Configuration.Output.WriteLineAsync();
        }
        else
        {
            await parseResult.Configuration.Error.WriteLineAsync("Authentication failed: Invalid or unauthorized token.");
        }
    }
}