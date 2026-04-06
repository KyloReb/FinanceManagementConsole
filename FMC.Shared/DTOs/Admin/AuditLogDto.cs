namespace FMC.Shared.DTOs.Admin;

public class AuditLogDto
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? Device { get; set; }
    public string? IpAddress { get; set; }
    public string? Organization { get; set; }
    
    // Financial enrichment
    public string? EntityName { get; set; }
    public decimal? Amount { get; set; }
    public string? Label { get; set; }
    public string? PerformedBy { get; set; }
    
    public DateTime CreatedAt { get; set; }
}
