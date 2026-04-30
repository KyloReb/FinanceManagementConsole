using FMC.Application.Interfaces;
using FMC.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FMC.Infrastructure.Services;

public class SystemAlertService : ISystemAlertService
{
    private readonly IApplicationDbContext _context;

    public SystemAlertService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task RaiseAlertAsync(string title, string message, AlertSeverity severity, string? entityId = null, string? entityType = null)
    {
        // Simple deduplication - don't raise the same active alert multiple times for the same entity
        var existing = await _context.SystemAlerts
            .AnyAsync(a => !a.IsResolved && a.Title == title && a.EntityId == entityId);
            
        if (existing) return;

        var alert = new SystemAlert
        {
            Title = title,
            Message = message,
            Severity = severity,
            EntityId = entityId,
            EntityType = entityType,
            CreatedAt = DateTime.UtcNow
        };

        _context.SystemAlerts.Add(alert);
        await _context.SaveChangesAsync();
    }

    public async Task<List<SystemAlert>> GetActiveAlertsAsync()
    {
        return await _context.SystemAlerts
            .Where(a => !a.IsResolved)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.CreatedAt)
            .Take(50)
            .ToListAsync();
    }

    public async Task ResolveAlertAsync(long id, string resolvedBy)
    {
        var alert = await _context.SystemAlerts.FindAsync(id);
        if (alert != null)
        {
            alert.IsResolved = true;
            alert.ResolvedBy = resolvedBy;
            alert.ResolvedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> GetUnresolvedCountAsync()
    {
        return await _context.SystemAlerts.CountAsync(a => !a.IsResolved);
    }
    
    public async Task CleanupOldAlertsAsync(int retentionDays)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        
        await _context.SystemAlerts
            .Where(a => a.IsResolved && a.ResolvedAt < cutoffDate)
            .ExecuteDeleteAsync();
    }
}
