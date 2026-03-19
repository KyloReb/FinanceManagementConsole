using FMC.Data;

namespace FMC.Models;

/// <summary>
/// Securely records generated One-Time Passwords (OTPs) mapping to user actions (e.g., Email Verification).
/// </summary>
public class UserOtpVerification
{
    /// <summary>
    /// Unique identifier for the verification session.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key reference to the ApplicationUser.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The secure 6-digit cryptographic code securely emailed to the user.
    /// </summary>
    public string OtpCode { get; set; } = string.Empty;

    /// <summary>
    /// Enum-style classification of the intent (e.g., EmailVerification, PasswordReset).
    /// </summary>
    public string OtpType { get; set; } = string.Empty;

    /// <summary>
    /// High-entropy expiration timestamp automatically enforcing lifecycle expiry.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Indicates whether this OTP was successfully verified to prevent replay attacks.
    /// </summary>
    public bool IsUsed { get; set; } = false;

    /// <summary>
    /// Security metric tracking failed verification attempts to block brute-force attacks.
    /// </summary>
    public int FailedAttempts { get; set; } = 0;

    /// <summary>
    /// Standard creation timestamp for audit logging.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Diagnostic mapping tracking the request origin IP Address.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Navigation property cleanly associating the OTP directly to the user record.
    /// </summary>
    public ApplicationUser? User { get; set; }

}
