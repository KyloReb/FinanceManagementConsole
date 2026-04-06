using FMC.Application.Interfaces;
using FMC.Shared.Auth;
using FMC.Shared.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FMC.Api.Controllers;

[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly ISystemAlertService _alertService;

    public AlertsController(ISystemAlertService alertService)
    {
        _alertService = alertService;
    }

    [HttpGet("active")]
    public async Task<ActionResult<List<SystemAlertDto>>> GetActive()
    {
        var alerts = await _alertService.GetActiveAlertsAsync();
        return Ok(alerts.Select(a => new SystemAlertDto
        {
            Id = a.Id,
            Title = a.Title,
            Message = a.Message,
            Severity = (AlertSeverityDto)a.Severity,
            IsResolved = a.IsResolved,
            CreatedAt = a.CreatedAt,
            EntityId = a.EntityId,
            EntityType = a.EntityType
        }).ToList());
    }

    [HttpGet("count")]
    public async Task<ActionResult<int>> GetCount()
    {
        return Ok(await _alertService.GetUnresolvedCountAsync());
    }

    [HttpPost("{id}/resolve")]
    public async Task<IActionResult> Resolve(long id)
    {
        await _alertService.ResolveAlertAsync(id, User.Identity?.Name ?? "System");
        return NoContent();
    }
}
