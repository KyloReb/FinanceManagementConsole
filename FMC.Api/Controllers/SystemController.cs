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
    private readonly ICacheService _cacheService;
    private readonly ILogger<SystemController> _logger;

    public SystemController(
        ISystemHealthService healthService, 
        IAuditService auditService,
        ICurrentUserService currentUserService,
        ICacheService cacheService,
        ILogger<SystemController> logger)
    {
        _healthService = healthService;
        _auditService = auditService;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
        _logger = logger;
    }

    [Authorize(Roles = Roles.SuperAdmin)]
    [HttpGet("maintenance")]
    public async Task<ActionResult<MaintenanceStatusDto>> GetMaintenanceStatus()
    {
        var isActive = await _cacheService.GetAsync<bool>("maintenance:mode");
        var message = await _cacheService.GetAsync<string>("maintenance:message");
        var activatedBy = await _cacheService.GetAsync<string>("maintenance:activated_by");
        var activatedAtStr = await _cacheService.GetAsync<string>("maintenance:activated_at");
        var blockedCount = await _cacheService.GetAsync<long>("maintenance:blocked_count");
        var scheduledAtStr = await _cacheService.GetAsync<string>("maintenance:scheduled_at");
        var scheduledMsg = await _cacheService.GetAsync<string>("maintenance:scheduled_message");

        return Ok(new MaintenanceStatusDto
        {
            IsActive = isActive,
            Message = message,
            ActivatedBy = activatedBy,
            ActivatedAt = !string.IsNullOrEmpty(activatedAtStr) ? DateTime.Parse(activatedAtStr, null, System.Globalization.DateTimeStyles.RoundtripKind) : null,
            BlockedCount = blockedCount,
            ScheduledAt = !string.IsNullOrEmpty(scheduledAtStr) ? DateTime.Parse(scheduledAtStr, null, System.Globalization.DateTimeStyles.RoundtripKind) : null,
            ScheduledMessage = scheduledMsg
        });
    }

    [Authorize(Roles = Roles.SuperAdmin)]
    [HttpPost("maintenance")]
    public async Task<ActionResult<MaintenanceStatusDto>> ToggleMaintenance([FromBody] MaintenanceToggleRequest request)
    {
        var userId = _currentUserService.UserId ?? "Unknown";
        var userName = User.Identity?.Name ?? "System";

        // Clear any pending schedule
        await _cacheService.RemoveAsync("maintenance:scheduled_at");
        await _cacheService.RemoveAsync("maintenance:scheduled_message");

        if (request.ScheduledAt.HasValue && request.ScheduledAt > DateTime.UtcNow && !request.IsActive)
        {
            await _cacheService.SetAsync("maintenance:scheduled_at", request.ScheduledAt.Value.ToString("O"), null);
            await _cacheService.SetAsync("maintenance:scheduled_message", request.ScheduledMessage ?? "System maintenance is scheduled.", null);

            await _auditService.RecordAuthEventAsync(
                action: "MAINTENANCE_SCHEDULED",
                userId: userId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString() ?? "N/A",
                device: Request.Headers["User-Agent"].ToString(),
                details: $"Maintenance scheduled at {request.ScheduledAt:yyyy-MM-dd HH:mm:ss} UTC by {userName}. Message: {request.ScheduledMessage ?? "None"}"
            );

            _logger.LogWarning("Maintenance SCHEDULED at {Time} by {User}", request.ScheduledAt, userName);
        }
        else if (request.IsActive)
        {
            await _cacheService.SetAsync("maintenance:mode", true, null);
            await _cacheService.SetAsync("maintenance:message", request.Message ?? "System is undergoing scheduled maintenance.", null);
            await _cacheService.SetAsync("maintenance:activated_by", userName, null);
            await _cacheService.SetAsync("maintenance:activated_at", DateTime.UtcNow.ToString("O"), null);
            await _cacheService.SetAsync("maintenance:blocked_count", 0L, null);

            await _auditService.RecordAuthEventAsync(
                action: "MAINTENANCE_ENABLED",
                userId: userId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString() ?? "N/A",
                device: Request.Headers["User-Agent"].ToString(),
                details: $"Maintenance mode activated by {userName}. Message: {request.Message ?? "None"}"
            );

            _logger.LogWarning("Maintenance mode ENABLED by {User}", userName);
        }
        else
        {
            await _cacheService.SetAsync("maintenance:mode", false, null);
            await _cacheService.RemoveAsync("maintenance:message");
            await _cacheService.SetAsync("maintenance:activated_by", userName, null);
            await _cacheService.SetAsync("maintenance:activated_at", DateTime.UtcNow.ToString("O"), null);

            await _auditService.RecordAuthEventAsync(
                action: "MAINTENANCE_DISABLED",
                userId: userId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString() ?? "N/A",
                device: Request.Headers["User-Agent"].ToString(),
                details: $"Maintenance mode deactivated by {userName}."
            );

            _logger.LogWarning("Maintenance mode DISABLED by {User}", userName);
        }

        return await GetMaintenanceStatus();
    }

    /// <summary>
    /// Retrieves a comprehensive real-time pulse of the system infrastructure.
    /// Restricted to SuperAdmins only.
    /// </summary>
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.SuperAdminApprover}")]
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
