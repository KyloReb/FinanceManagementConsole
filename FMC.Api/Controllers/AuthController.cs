using FMC.Application.Interfaces;
using FMC.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FMC.Api.Controllers;

/// <summary>
/// Handles all authentication-related requests including login, token refresh, and logout.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly IAuditService _auditService;

    public AuthController(IIdentityService identityService, IAuditService auditService)
    {
        _identityService = identityService;
        _auditService = auditService;
    }

    /// <summary>
    /// Authenticates a user and returns a session-hardened JWT and refresh token.
    /// </summary>
    /// <param name="request">The login credentials and optional OTP.</param>
    /// <returns>An AuthResponseDto on success; 401 Unauthorized on failure.</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _identityService.LoginAsync(request);
        if (result == null)
        {
            // Log Failed Login Attempt (Security Forensic)
            await _auditService.RecordAuthEventAsync("Login Failed", null, ip, userAgent, $"Failed attempt for: {request.Identifier}");
            return Unauthorized(new { message = "Invalid email or password." });
        }

        // Record Successful Login
        await _auditService.RecordAuthEventAsync("Login Success", result.UserId, ip, userAgent, "Successful login session established");

        // Set the refresh token as an HTTP-only cookie for a session-hardened flow
        SetRefreshTokenCookie(result.RefreshToken);

        return Ok(result);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        var result = await _identityService.RegisterAsync(request);
        if (!result) return BadRequest(new { message = "Registration failed. Email or Username may already be in use." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = Request.Headers.UserAgent.ToString();
        await _auditService.RecordAuthEventAsync("Registration", null, ip, userAgent, $"New user registered: {request.Email}");

        return Ok(new { message = "Registration successful. Please check your email for the verification code." });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequestDto request)
    {
        var result = await _identityService.VerifyEmailAsync(request);
        if (!result) return BadRequest(new { message = "Invalid or expired verification code." });
        return Ok(new { message = "Email verified successfully." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        var result = await _identityService.ForgotPasswordAsync(request);
        // We always return OK to prevent email enumeration, but with a generic message if null
        if (result == null)
            return Ok(new { message = "If the account exists, a password reset code has been sent." });

        return Ok(new { 
            message = "A verification code has been sent to your email.",
            maskedEmail = result.MaskedEmail,
            userId = result.UserId
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        var result = await _identityService.ResetPasswordAsync(request);
        if (!result) return BadRequest(new { message = "Invalid code or password requirements not met." });

        // Optionally record audit
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = Request.Headers.UserAgent.ToString();
        await _auditService.RecordAuthEventAsync("Password Reset", request.UserId, ip, userAgent, "Password Reset via OTP successfully completed");

        return Ok(new { message = "Password reset successfully. You can now log in." });
    }

    /// <summary>
    /// Renews an expired access token using the refresh token stored in the HTTP-only cookie.
    /// </summary>
    /// <returns>A new AuthResponseDto with a fresh JWT.</returns>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken)) return Unauthorized();

        var result = await _identityService.RefreshTokenAsync(refreshToken);
        if (result == null) return Unauthorized();

        SetRefreshTokenCookie(result.RefreshToken);
        return Ok(result);
    }

    /// <summary>
    /// Invalidates the user's current session by revoking the refresh token.
    /// </summary>
    /// <param name="userId">The ID of the user logging out.</param>
    /// <returns>200 OK on success.</returns>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromQuery] string userId)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = Request.Headers.UserAgent.ToString();
        await _auditService.RecordAuthEventAsync("Logout", userId, ip, userAgent, "User Logout Command triggered");

        await _identityService.LogoutAsync(userId);
        Response.Cookies.Delete("refreshToken");
        return Ok();
    }

    [Authorize]
    [HttpPost("change-password/initiate")]
    public async Task<IActionResult> InitiatePasswordChange([FromBody] ChangePasswordRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _identityService.InitiatePasswordChangeAsync(userId, request);
        if (!result) return BadRequest(new { message = "Invalid current password." });

        return Ok(new { message = "Security code sent to your email." });
    }

    [Authorize]
    [HttpPost("change-password/complete")]
    public async Task<IActionResult> CompletePasswordChange([FromBody] VerifyPasswordChangeDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _identityService.CompletePasswordChangeAsync(userId, request);
        if (!result) return BadRequest(new { message = "Invalid security code or password requirements not met." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = Request.Headers.UserAgent.ToString();
        await _auditService.RecordAuthEventAsync("Password Changed", userId, ip, userAgent, "Password Changed Successfully");

        return Ok(new { message = "Password updated successfully." });
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // Ensure this is only sent over HTTPS
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(7)
        };
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }
}
