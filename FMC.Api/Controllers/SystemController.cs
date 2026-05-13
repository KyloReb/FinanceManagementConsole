using FMC.Application.Interfaces;
using FMC.Shared.Auth;
using FMC.Shared.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace FMC.Api.Controllers;

/// <summary>
/// Controller responsible for exposing system-level diagnostic and health information.
/// Access is strictly restricted to SuperAdmins to prevent infrastructure leak.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly ISystemHealthService _healthService;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SystemController> _logger;

    public SystemController(
        ISystemHealthService healthService, 
        IAuditService auditService,
        ICurrentUserService currentUserService,
        ILogger<SystemController> logger)
    {
        _healthService = healthService;
        _auditService = auditService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a comprehensive real-time pulse of the system infrastructure.
    /// Restricted to SuperAdmins only.
    /// </summary>
    [Authorize(Roles = Roles.SuperAdmin)]
    [HttpGet("health-pulse")]
    public async Task<ActionResult<SystemHealthDto>> GetHealthPulse(CancellationToken cancellationToken)
    {
        try
        {
            var pulse = await _healthService.GetSystemHealthPulseAsync(cancellationToken);
            return Ok(pulse);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve system health pulse.");
            return StatusCode(500, "Diagnostic capture failed.");
        }
    }

    /// <summary>
    /// Receives a client-side error report and logs it to the System Activity Log.
    /// Available to all authenticated users.
    /// </summary>
    [HttpPost("report-error")]
    public async Task<IActionResult> ReportError([FromBody] ClientErrorCommandDto error)
    {
        var userName = User.Identity?.Name ?? "Unknown User";
        var userId = _currentUserService.UserId;
        
        _logger.LogWarning("Client-side error reported by {User}: {Message}", userName, error.Message);

        await _auditService.RecordAuthEventAsync(
            action: "CLIENT_CRASH",
            userId: userId,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString() ?? "N/A",
            device: Request.Headers["User-Agent"].ToString(),
            details: $"[User: {userName}] [Component: {error.Component ?? "N/A"}] {error.Message} | Trace: {error.StackTrace}"
        );

        return Ok();
    }
}
