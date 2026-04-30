namespace FMC.Shared.DTOs;

public class BulkTransactionRowDto
{
    public int RowNumber { get; set; }
    public string Subscriber { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty; 
    public decimal Amount { get; set; }

    // Set server-side after matching
    public Guid? ResolvedUserId { get; set; }
    public string? ValidationError { get; set; }
}
