using FMC.Application.Interfaces;
using FMC.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Mvc;

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
            await _auditService.RecordLoginAsync(null, ip, $"{userAgent} | Failed attempt for: {request.Identifier}");
            return Unauthorized(new { message = "Invalid email or password." });
        }

        // Record Successful Login
        await _auditService.RecordLoginAsync(result.UserId, ip, userAgent);

        // Set the refresh token as an HTTP-only cookie for a session-hardened flow
        SetRefreshTokenCookie(result.RefreshToken);

        return Ok(result);
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
        await _auditService.RecordLoginAsync(userId, ip, "User Logout Command");

        await _identityService.LogoutAsync(userId);
        Response.Cookies.Delete("refreshToken");
        return Ok();
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
