using FMC.Domain.Entities;

namespace FMC.Application.Interfaces;

public interface ISystemAlertService
{
    Task RaiseAlertAsync(string title, string message, AlertSeverity severity, string? entityId = null, string? entityType = null);
    Task<List<SystemAlert>> GetActiveAlertsAsync();
    Task ResolveAlertAsync(long id, string resolvedBy);
    Task<int> GetUnresolvedCountAsync();
    Task CleanupOldAlertsAsync(int retentionDays);
}
