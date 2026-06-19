namespace FMC.Shared.DTOs.Admin;

public class MaintenanceStatusDto
{
    public bool IsActive { get; set; }
    public string? Message { get; set; }
    public string? ActivatedBy { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public long BlockedCount { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string? ScheduledMessage { get; set; }
    public string? ModeType { get; set; }
    public int GraceMinutes { get; set; }
}

public class MaintenanceToggleRequest
{
    public bool IsActive { get; set; }
    public string? Message { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string? ScheduledMessage { get; set; }
    public string? ModeType { get; set; }
    public int GraceMinutes { get; set; }
}
