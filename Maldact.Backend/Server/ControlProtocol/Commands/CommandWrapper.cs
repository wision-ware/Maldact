using System.CommandLine;
using System.CommandLine.Parsing;
using Maldact.Core.Server;

namespace Maldact.Backend.Server.ControlProtocol.Commands;

/// <summary>
/// Wraps the shared command data
/// </summary>
/// <param name="context">The session context for the command execution.</param>
internal abstract class CommandWrapper(SessionContext context)
{
    /// <summary>
    /// The session context for the command execution.
    /// </summary>
    protected readonly SessionContext Context = context;
    
    /// <summary>
    /// The command definition itself.
    /// </summary>
    public abstract Command Command { get; protected set; }
    
    /// <summary>
    /// The common error message for denying access in case of missing authorization.
    /// </summary>
    protected const string UnauthorizedAccessMessage =  "Unauthorized command execution!";
}