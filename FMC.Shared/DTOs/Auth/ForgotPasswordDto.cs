using System.ComponentModel.DataAnnotations;

namespace FMC.Shared.DTOs.Auth;

public class ForgotPasswordRequestDto
{
    [Required]
    [Display(Name = "Username or Email")]
    public string Identifier { get; set; } = string.Empty;
}

public class ForgotPasswordResponseDto
{
    public string MaskedEmail { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}

public class ResetPasswordRequestDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [Display(Name = "6-Digit Code")]
    [StringLength(6, MinimumLength = 6)]
    public string Otp { get; set; } = string.Empty;

    [Required]
    [Display(Name = "New Password")]
    [MinLength(6)]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Confirm New Password")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
    [DataType(DataType.Password)]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
