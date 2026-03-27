using FMC.Application.Interfaces;
using FMC.Shared.Auth;
using FMC.Shared.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FMC.Api.Controllers;

[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet("auth-logs")]
    public async Task<ActionResult<List<AuditLogDto>>> GetAuthLogs()
    {
        return Ok(await _auditService.GetAuthLogsAsync());
    }

    // Keep for backward compatibility if needed, but points to the same logic
    [HttpGet("login-logs")]
    public async Task<ActionResult<List<AuditLogDto>>> GetLoginLogs()
    {
        return Ok(await _auditService.GetAuthLogsAsync());
    }
}
