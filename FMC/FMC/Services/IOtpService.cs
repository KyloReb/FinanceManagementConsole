namespace FMC.Services;

/// <summary>
/// Defines the contract for generating, storing, and validating One-Time Passwords (OTPs).
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Generates a 6-digit OTP, stores it in the database for the user, and emails it to them.
    /// </summary>
    /// <param name="userId">The ID of the user requesting the OTP.</param>
    /// <param name="email">The email address to send the OTP to.</param>
    /// <param name="otpType">The type of OTP (e.g., 'EmailVerification', 'Login2FA').</param>
    /// <returns>The generated 6-digit code.</returns>
    Task<string> GenerateAndSendOtpAsync(string userId, string email, string otpType);

    /// <summary>
    /// Verifies if a submitted OTP is valid, un-used, and not expired for the given user.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="code">The 6-digit code submitted by the user.</param>
    /// <param name="otpType">The classification of OTP being verified.</param>
    /// <returns>True if the OTP is valid and consumed; otherwise, false.</returns>
    Task<bool> VerifyOtpAsync(string userId, string code, string otpType);
}
