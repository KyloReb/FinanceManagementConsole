using FMC.Application.Interfaces;
using FMC.Shared.Auth;
using FMC.Shared.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FMC.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly ISystemAlertService _alertService;
    private readonly IOrganizationService _orgService;
    private readonly ICurrentUserService _currentUserService;

    public AlertsController(
        ISystemAlertService alertService, 
        IOrganizationService orgService,
        ICurrentUserService currentUserService)
    {
        _alertService = alertService;
        _orgService = orgService;
        _currentUserService = currentUserService;
    }

    [HttpGet("active")]
    public async Task<ActionResult<List<SystemAlertDto>>> GetActive()
    {
        var isSuperAdmin = User.IsInRole(Roles.SuperAdmin);
        var tenantId = _currentUserService.TenantId;

        var alerts = await _alertService.GetActiveAlertsAsync();
        
        // Filter by Tenant
        if (!isSuperAdmin)
        {
            alerts = alerts.Where(a => a.TenantId == tenantId || a.TenantId == "GLOBAL").ToList();
        }

        var dtos = alerts
            .Where(a => !a.Message.Contains("Nationlink/Infoserve Inc."))
            .Select(a => new SystemAlertDto
        {
            Id = a.Id,
            Title = a.Title,
            Message = a.Message,
            Severity = (AlertSeverityDto)a.Severity,
            IsResolved = a.IsResolved,
            CreatedAt = a.CreatedAt,
            EntityId = a.EntityId,
            EntityType = a.EntityType
        }).ToList();

        // ── Dynamic Organization Capacity Threshold Alerts ──
        if (isSuperAdmin)
        {
            var orgs = await _orgService.GetAllAsync();
            foreach (var org in orgs.Where(o => o.Name != null && !o.Name.Contains("Nationlink", StringComparison.OrdinalIgnoreCase)))
            {
                var usedPct = org.TotalBalance > 0 ? (org.Usage / org.TotalBalance) * 100m : 0m;
                var orgBalance = org.TotalBalance - org.Usage;

                if (usedPct >= 80m || orgBalance <= 100_000m)
                {
                    AddDynamicAlert(dtos, org, usedPct, orgBalance);
                }
            }
        }
        else if (!string.IsNullOrEmpty(tenantId))
        {
            // Individual Org Alert for CEO/Maker
            if (Guid.TryParse(tenantId, out var orgId))
            {
                var org = await _orgService.GetByIdAsync(orgId);
                if (org != null)
                {
                    var usedPct = org.TotalBalance > 0 ? (org.Usage / org.TotalBalance) * 100m : 0m;
                    var orgBalance = org.TotalBalance - org.Usage;
                    if (usedPct >= 80m || orgBalance <= 100_000m)
                    {
                        AddDynamicAlert(dtos, org, usedPct, orgBalance);
                    }
                }
            }
        }

        return Ok(dtos.OrderByDescending(d => d.CreatedAt).ToList());
    }

    private void AddDynamicAlert(List<SystemAlertDto> dtos, FMC.Shared.DTOs.Organization.OrganizationDto org, decimal usedPct, decimal orgBalance)
    {
        bool hasExisting = dtos.Any(d => d.EntityId == org.Id.ToString() && (d.Title.Contains("Liquidity") || d.Title.Contains("Capacity")));
        if (hasExisting) return;

        dtos.Insert(0, new SystemAlertDto
        {
            Id = 0,
            Title = "Capacity Threshold",
            Message = usedPct >= 80m ? $"{org.Name} has {usedPct:F1}% of wallet allocated." : $"{org.Name} liquidity is low: {orgBalance:C}",
            Severity = usedPct >= 80m ? AlertSeverityDto.Security : AlertSeverityDto.Warning,
            EntityType = "Organization",
            EntityId = org.Id.ToString(),
            CreatedAt = DateTime.UtcNow
        });
    }

    [HttpGet("count")]
    public async Task<ActionResult<int>> GetCount()
    {
        var active = await GetActive();
        if (active.Result is OkObjectResult ok)
        {
            var list = ok.Value as List<SystemAlertDto>;
            return Ok(list?.Count ?? 0);
        }
        return Ok(0);
    }

    [HttpPost("{id}/resolve")]
    public async Task<IActionResult> Resolve(long id)
    {
        // Resolve check: ensure user owns the alert or is SuperAdmin
        if (!User.IsInRole(Roles.SuperAdmin))
        {
            var alerts = await _alertService.GetActiveAlertsAsync();
            var alert = alerts.FirstOrDefault(a => a.Id == id);
            if (alert != null && alert.TenantId != _currentUserService.TenantId && alert.TenantId != "GLOBAL")
            {
                return Forbid();
            }
        }

        await _alertService.ResolveAlertAsync(id, User.Identity?.Name ?? "System");
        return NoContent();
    }
}
