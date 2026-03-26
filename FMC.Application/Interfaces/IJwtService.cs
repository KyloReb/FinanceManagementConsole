namespace FMC.Application.Interfaces;

/// <summary>
/// Defines the contract for generating JWT access tokens and managing user identity context.
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Generates a session-hardened JWT for the specified user context.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="firstName">The user's first name.</param>
    /// <param name="lastName">The user's last name.</param>
    /// <param name="roles">The roles assigned to the user.</param>
    /// <returns>A string representation of the signed JWT.</returns>
    string GenerateToken(string userId, string email, string? firstName, string? lastName, IEnumerable<string> roles);

    /// <summary>
    /// Generates a high-entropy refresh token string.
    /// </summary>
    /// <returns>A secure random token string.</returns>
    string GenerateRefreshToken();
}
