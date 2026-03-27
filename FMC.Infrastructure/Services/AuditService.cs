using FMC.Application.Interfaces;
using FMC.Domain.Entities;
using FMC.Shared.DTOs.Admin;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FMC.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly IApplicationDbContext _context;
    private readonly Microsoft.AspNetCore.Identity.UserManager<FMC.Infrastructure.Data.ApplicationUser> _userManager;

    public AuditService(IApplicationDbContext context, Microsoft.AspNetCore.Identity.UserManager<FMC.Infrastructure.Data.ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task RecordAuthEventAsync(string action, string? userId, string ipAddress, string details)
    {
        var log = new AuditLog
        {
            UserId = userId,
            TenantId = userId ?? "SYSTEM", // Explicitly group unauthenticated/failed events under 'SYSTEM'
            Action = action,
            IpAddress = ipAddress,
            Details = details,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task<List<AuditLogDto>> GetAuthLogsAsync()
    {
        // Now filtering for the new Action strings we might use, or all auth-related actions.
        var authActions = new[] { "Login Success", "Login Failed", "Logout", "Password Reset", "Password Changed", "Registration" };

        var logs = await _context.AuditLogs
            .IgnoreQueryFilters() // SuperAdmin needs to see global history
            .Where(a => a.Action == "Login" || authActions.Contains(a.Action)) // Keep "Login" for backward compatibility
            .OrderByDescending(a => a.CreatedAt)
            .Take(500)
            .ToListAsync();

        var dtos = new List<AuditLogDto>();
        foreach (var log in logs)
        {
            var user = log.UserId != null ? await _userManager.FindByIdAsync(log.UserId) : null;
            dtos.Add(new AuditLogDto
            {
                Id = log.Id,
                UserId = log.UserId,
                UserName = user?.UserName ?? "Unknown",
                Action = log.Action,
                IpAddress = log.IpAddress,
                Details = log.Details,
                CreatedAt = log.CreatedAt
            });
        }
        return dtos;
    }
}
