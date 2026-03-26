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

    [HttpGet("login-logs")]
    public async Task<ActionResult<List<AuditLogDto>>> GetLoginLogs()
    {
        return Ok(await _auditService.GetLoginLogsAsync());
    }
}
