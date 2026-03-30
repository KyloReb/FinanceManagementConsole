using Microsoft.AspNetCore.Identity;

namespace FMC.Infrastructure.Data;

/// <summary>
/// Extended ApplicationUser inheriting from IdentityUser to support custom profile data and lifecycle events.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// The user's provided first name. Optional.
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// The user's provided last name. Optional.
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Indicates whether the user's account is active. If false, login is blocked.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp recording precisely when the user's account was registered.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp recording the last successful login event. Updates on every login.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// The high-entropy refresh token used for session renewal.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// The expiration timestamp for the refresh token.
    /// </summary>
    public DateTime? RefreshTokenExpiryTime { get; set; }

    /// <summary>
    /// The user's affiliated organization or company.
    /// </summary>
    public string? Organization { get; set; }
}
