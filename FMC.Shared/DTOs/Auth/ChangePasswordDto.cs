namespace FMC.Shared.DTOs.Auth;

public class ChangePasswordRequestDto
{
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class VerifyPasswordChangeDto
{
    public string NewPassword { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
}
