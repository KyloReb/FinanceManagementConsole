namespace FMC.Shared.DTOs.Admin;

public class MaintenanceHistoryItem
{
    public long Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? PerformedBy { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
}
