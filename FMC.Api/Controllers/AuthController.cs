using FMC.Application.Interfaces;
using FMC.Domain.Entities;
using FMC.Shared.DTOs.Auth;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using static FMC.Application.Interfaces.RateLimitPolicies;

namespace FMC.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("AuthPolicy")]
public class AuthController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly IAuditService _auditService;
    private readonly IAuthRateLimitService _rateLimit;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBackgroundJobClient _jobClient;

    public AuthController(
        IIdentityService identityService,
        IAuditService auditService,
        IAuthRateLimitService rateLimit,
        UserManager<ApplicationUser> userManager,
        IBackgroundJobClient jobClient)
    {
        _identityService = identityService;
        _auditService = auditService;
        _rateLimit = rateLimit;
        _userManager = userManager;
        _jobClient = jobClient;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var clientId = $"{request.Identifier}|{ip}";

        var rateCheck = await _rateLimit.CheckAsync(clientId);
        if (!rateCheck.IsAllowed)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = rateCheck.LockoutReason ?? "Too many login attempts.",
                retryAfter = rateCheck.RetryAfterSeconds,
                remainingAttempts = 0
            });
        }

        var userAgent = Request.Headers.UserAgent.ToString();
        var result = await _identityService.LoginAsync(request);

        if (result == null)
        {
            await _rateLimit.RecordAttemptAsync(clientId, false);
            string eventType = request.IsStepUp ? "Step-Up Verification Failed" : "Login Failed";
            await _auditService.RecordAuthEventAsync(eventType, null, ip, userAgent, $"Failed attempt for: {request.Identifier}");

            // ── Suspicious login detection ──
            // Only send when all 5 attempts in the current window are exhausted
            // (remaining == 0). This means at most 1 email per lockout window per user.
            var remainingAfterAttempt = Math.Max(0, rateCheck.RemainingAttempts - 1);
            if (remainingAfterAttempt == 0)
            {
                var user = await _userManager.FindByEmailAsync(request.Identifier)
                           ?? await _userManager.FindByNameAsync(request.Identifier);
                if (user?.Email != null)
                {
                    var totalAttempts = 5;
                    _jobClient.Enqueue<FMC.Infrastructure.BackgroundJobs.NotificationJobService>(job =>
                        job.SendSuspiciousLoginAlertAsync(
                            user.Email,
                            $"{user.FirstName} {user.LastName}".Trim(),
                            ip,
                            userAgent,
                            totalAttempts,
                            remainingAfterAttempt));
                }
            }

            return Unauthorized(new
            {
                message = "Invalid email or password.",
                remainingAttempts = remainingAfterAttempt
            });
        }

        await _rateLimit.ClearAsync(clientId);
        string successEvent = request.IsStepUp ? "Step-Up Verification Success" : "Login Success";
        await _auditService.RecordAuthEventAsync(successEvent, result.UserId, ip, userAgent, "Successful authentication session");

        SetRefreshTokenCookie(result.RefreshToken);
        return Ok(result);
    }

    // Public Registration Endpoint has been removed. 
    // New users must be created by a SuperAdmin or CEO via secure administration interfaces.

    // Email Verification endpoint has been removed as accounts are now verified by default upon administrative creation.

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var clientId = $"{request.Identifier}|{ip}";

        var rateCheck = await _rateLimit.CheckAsync(clientId, RateLimitPolicies.ForgotPassword);
        if (!rateCheck.IsAllowed)
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "Too many password reset requests.", retryAfter = rateCheck.RetryAfterSeconds });

        var result = await _identityService.ForgotPasswordAsync(request);
        if (result == null)
        {
            await _rateLimit.RecordAttemptAsync(clientId, false, RateLimitPolicies.ForgotPassword);
            return Ok(new { message = "If the account exists, a password reset code has been sent." });
        }

        await _rateLimit.ClearAsync(clientId);
        return Ok(new
        {
            message = "A verification code has been sent to your email.",
            maskedEmail = result.MaskedEmail,
            userId = result.UserId
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var clientId = $"{request.UserId}|{ip}";

        var rateCheck = await _rateLimit.CheckAsync(clientId, RateLimitPolicies.ResetPassword);
        if (!rateCheck.IsAllowed)
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "Too many reset attempts.", retryAfter = rateCheck.RetryAfterSeconds });

        var result = await _identityService.ResetPasswordAsync(request);
        if (!result)
        {
            await _rateLimit.RecordAttemptAsync(clientId, false, RateLimitPolicies.ResetPassword);
            return BadRequest(new { message = "Invalid code or password requirements not met." });
        }

        await _rateLimit.ClearAsync(clientId);
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = Request.Headers.UserAgent.ToString();
        await _auditService.RecordAuthEventAsync("Password Reset", request.UserId, auditIp, userAgent, "Password Reset via OTP successfully completed");

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
    /// <param name="userId">The optional ID of the user logging out. If omitted, current user is used.</param>
    /// <returns>200 OK on success.</returns>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromQuery] string? userId)
    {
        var targetId = userId;
        // Resolve from the Actual authenticated User claims
        if (string.IsNullOrEmpty(targetId) || targetId == "current")
        {
            targetId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = Request.Headers.UserAgent.ToString();
        
        await _auditService.RecordAuthEventAsync("Logout", targetId, ip, userAgent, "User Logout Command triggered");

        if (!string.IsNullOrEmpty(targetId))
        {
            await _identityService.LogoutAsync(targetId);
        }
        
        Response.Cookies.Delete("refreshToken");
        return Ok();
    }

    [Authorize]
    [HttpPost("change-password/initiate")]
    public async Task<IActionResult> InitiatePasswordChange([FromBody] ChangePasswordRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var clientId = $"{userId}|{ip}";

        var rateCheck = await _rateLimit.CheckAsync(clientId, ChangePassword);
        if (!rateCheck.IsAllowed)
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "Too many password change attempts.", retryAfter = rateCheck.RetryAfterSeconds });

        var result = await _identityService.InitiatePasswordChangeAsync(userId, request);
        if (!result)
        {
            await _rateLimit.RecordAttemptAsync(clientId, false, ChangePassword);
            return BadRequest(new { message = "Invalid current password." });
        }

        await _rateLimit.ClearAsync(clientId);
        return Ok(new { message = "Security code sent to your email." });
    }

    [Authorize]
    [HttpPost("change-password/complete")]
    public async Task<IActionResult> CompletePasswordChange([FromBody] VerifyPasswordChangeDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var clientId = $"{userId}|{ip}";

        var rateCheck = await _rateLimit.CheckAsync(clientId, ChangePassword);
        if (!rateCheck.IsAllowed)
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "Too many password change attempts.", retryAfter = rateCheck.RetryAfterSeconds });

        var result = await _identityService.CompletePasswordChangeAsync(userId, request);
        if (!result)
        {
            await _rateLimit.RecordAttemptAsync(clientId, false, ChangePassword);
            return BadRequest(new { message = "Invalid security code or password requirements not met." });
        }

        await _rateLimit.ClearAsync(clientId);
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = Request.Headers.UserAgent.ToString();
        await _auditService.RecordAuthEventAsync("Password Changed", userId, auditIp, userAgent, "Password Changed Successfully");

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
