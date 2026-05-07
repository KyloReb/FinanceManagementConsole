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
    private readonly IOrganizationService _orgService;

    public AlertsController(ISystemAlertService alertService, IOrganizationService orgService)
    {
        _alertService = alertService;
        _orgService = orgService;
    }

    [HttpGet("active")]
    public async Task<ActionResult<List<SystemAlertDto>>> GetActive()
    {
        var alerts = await _alertService.GetActiveAlertsAsync();
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

        // ── Dynamic Organization Capacity Threshold Alerts for SuperAdmin ──
        var orgs = await _orgService.GetAllAsync();
        foreach (var org in orgs.Where(o => o.Name != null && !o.Name.Contains("Nationlink", StringComparison.OrdinalIgnoreCase)))
        {
            var orgBalance = org.TotalBalance - org.Usage;
            var usedPct = org.TotalBalance > 0 ? (org.Usage / org.TotalBalance) * 100m : 0m;

            if (usedPct >= 80m || orgBalance <= 100_000m)
            {
                // Deduplication: Skip if a persisted liquidity/capacity alert already exists for this organization
                bool hasExistingMoneyAlert = dtos.Any(d => d.EntityId == org.Id.ToString() && 
                    (d.Title.Contains("Liquidity") || d.Title.Contains("Capacity")));
                
                if (hasExistingMoneyAlert) continue;

                var msg = usedPct >= 80m ? $"{org.Name} has {usedPct:F1}% of wallet allocated." : $"{org.Name} only has {orgBalance:C} remaining in org wallet.";
                var sev = usedPct >= 80m ? AlertSeverityDto.Security : AlertSeverityDto.Warning;
                
                dtos.Insert(0, new SystemAlertDto
                {
                    Id = 0, // Synthetic ID to bypass db resolve
                    Title = "Capacity Threshold",
                    Message = msg,
                    Severity = sev,
                    EntityType = "Organization",
                    EntityId = org.Id.ToString(),
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        return Ok(dtos);
    }

    [HttpGet("count")]
    public async Task<ActionResult<int>> GetCount()
    {
        var alerts = await _alertService.GetActiveAlertsAsync();
        var persistedCount = alerts.Count(a => !a.Message.Contains("Nationlink/Infoserve Inc."));
        
        var orgs = await _orgService.GetAllAsync();
        var dynamicCount = orgs.Where(o => o.Name != null && !o.Name.Contains("Nationlink", StringComparison.OrdinalIgnoreCase)).Count(org => 
        {
            var usedPct = org.TotalBalance > 0 ? (org.Usage / org.TotalBalance) * 100m : 0m;
            var orgBalance = org.TotalBalance - org.Usage;
            return usedPct >= 80m || orgBalance <= 100_000m;
        });

        return Ok(persistedCount + dynamicCount);
    }

    [HttpPost("{id}/resolve")]
    public async Task<IActionResult> Resolve(long id)
    {
        await _alertService.ResolveAlertAsync(id, User.Identity?.Name ?? "System");
        return NoContent();
    }
}
