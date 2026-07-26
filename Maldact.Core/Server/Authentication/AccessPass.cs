namespace Maldact.Core.Server.Authentication;

/// <summary>
/// Represents an authenticated user's authorization claims and identity within the server runtime.
/// </summary>
public sealed class AccessPass : IEquatable<AccessPass>
{
    /// <summary>
    /// Defines the hierarchical permission levels available to a user.
    /// </summary>
    public enum Role
    {
        User,
        Admin
    }

    /// <summary>
    /// Strongly typed wrapper for the unique access pass identifier.
    /// </summary>
    public readonly record struct PassId(string Value);

    /// <summary>
    /// Initializes a new access pass with the specified role and generates a unique identifier.
    /// </summary>
    /// <param name="role">The permission level to grant.</param>
    public AccessPass(Role role)
    {
        AccessRole = role;
        Id = new PassId(Guid.NewGuid().ToString());
    }
    
    /// <summary>
    /// Gets the authorization level granted to this pass.
    /// </summary>
    public Role AccessRole { get; }

    /// <summary>
    /// Gets the globally unique identifier for this specific pass instance.
    /// </summary>
    public PassId Id { get; }

    /// <inheritdoc />
    public bool Equals(AccessPass? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id.Equals(other.Id);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is AccessPass other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Id.GetHashCode();
}