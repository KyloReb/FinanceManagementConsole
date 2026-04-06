namespace FMC.Shared.DTOs.Admin;

public enum AlertSeverityDto
{
    Information,
    Warning,
    Critical,
    Security
}

public class SystemAlertDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverityDto Severity { get; set; }
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? EntityId { get; set; }
    public string? EntityType { get; set; }
}
