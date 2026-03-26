using FMC.Domain.Common;

namespace FMC.Domain.Entities;

/// <summary>
/// A centralized logging schema representing all application lifecycle and security events.
/// </summary>
public class AuditLog : ITenantEntity
{
    /// <summary>
    /// Auto-incrementing primary key for tracking log sequence chronologically.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Tenant identifier for data isolation.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// User performing the secure action, strictly null if anonymous boundary.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Categorical intent label describing the action (e.g., Login, OtpSent, OtpFailed).
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// The physical table or context classification altered by the action.
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// The physical database ID of the altered entity context.
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// Serialized JSON string preserving dynamic metadata context.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Telemetry IP Address for geographic security bounding.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Timestamp declaring immediately when the log action was committed to the datastore.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
