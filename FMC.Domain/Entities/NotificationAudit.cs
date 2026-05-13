namespace FMC.Domain.Entities;

/// <summary>
/// Audit record for a single notification dispatch.
/// Provides granular idempotency — prevents duplicate emails even across
/// extreme Hangfire retries or server restarts.
/// </summary>
public class NotificationAudit
{
    public long Id { get; set; }

    /// <summary>
    /// Unique fingerprint: "{ActionType}:{EntityId}:{Recipient}".
    /// A database UNIQUE index on this column is the idempotency guarantee.
    /// </summary>
    public string NotificationKey { get; set; } = string.Empty;

    public string Recipient { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string? EntityId { get; set; }

    /// <summary>Provider message ID (e.g. SMTP message-id) for delivery tracing.</summary>
    public string? ProviderMessageId { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "SENT";
    public string? ErrorMessage { get; set; }
}
