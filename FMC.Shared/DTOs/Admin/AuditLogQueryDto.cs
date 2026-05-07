namespace FMC.Shared.DTOs.Admin;

public class AuditLogQueryDto
{
    public string? Action { get; set; }
    public string? Category { get; set; } // "auth" | "financial" | null for all
    public string? PerformedBy { get; set; }
    public string? EntityName { get; set; }
    public string? TenantId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class AuditLogSearchResultDto
{
    public List<AuditLogDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
}
