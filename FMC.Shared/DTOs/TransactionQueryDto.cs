namespace FMC.Shared.DTOs;

public class TransactionQueryDto
{
    public Guid? OrganizationId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Status { get; set; }
    public string? Category { get; set; }
    public int Count { get; set; } = 2000;
}
