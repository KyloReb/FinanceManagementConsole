using System.ComponentModel.DataAnnotations;

namespace FMC.Shared.DTOs.Auth;

/// <summary>
/// Represents a login attempt including credentials and optional MFA/OTP code.
/// </summary>
public class LoginRequestDto
{
    [Required]
    [Display(Name = "Username or Email")]
    public string Identifier { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
    public bool IsStepUp { get; set; }

    public string? OtpCode { get; set; }
}
