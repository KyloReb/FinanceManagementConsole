using FMC.Domain.Common;

namespace FMC.Domain.Entities;

public enum AlertSeverity
{
    Information,
    Warning,
    Critical,
    Security
}

public class SystemAlert : ITenantEntity
{
    public long Id { get; set; }
    public string TenantId { get; set; } = "SYSTEM";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public bool IsResolved { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Optional link to a specific entity
    public string? EntityId { get; set; }
    public string? EntityType { get; set; }
}
