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

    [HttpGet("logs")]
    public async Task<ActionResult<List<AuditLogDto>>> GetRecentLogs([FromQuery] int count = 20, [FromQuery] string? category = null)
    {
        return Ok(await _auditService.GetRecentLogsAsync(count, category));
    }

    [HttpPost("search")]
    public async Task<ActionResult<AuditLogSearchResultDto>> SearchLogs([FromBody] AuditLogQueryDto query)
    {
        return Ok(await _auditService.SearchLogsAsync(query));
    }
}
