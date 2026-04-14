using FMC.Application.Interfaces;
using FMC.Shared.Auth;
using FMC.Shared.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FMC.Api.Controllers;

[Authorize(Roles = Roles.SuperAdmin + "," + Roles.CEO + "," + Roles.Maker + "," + Roles.Approver)]
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
    public async Task<ActionResult<List<AuditLogDto>>> GetRecentLogs([FromQuery] int count = 20, [FromQuery] string? category = null, [FromQuery] string? tenantId = null)
    {
        // Security: Non-SuperAdmins can only see their own organization logs
        if (!User.IsInRole(Roles.SuperAdmin))
        {
            var userOrgClaim = User.FindFirst("OrganizationId")?.Value;
            if (!string.IsNullOrEmpty(userOrgClaim))
            {
                tenantId = userOrgClaim;
            }
            else
            {
                return Forbid();
            }
        }

        return Ok(await _auditService.GetRecentLogsAsync(count, category, tenantId));
    }

    [HttpPost("search")]
    public async Task<ActionResult<AuditLogSearchResultDto>> SearchLogs([FromBody] AuditLogQueryDto query)
    {
        return Ok(await _auditService.SearchLogsAsync(query));
    }
}
