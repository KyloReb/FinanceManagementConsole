namespace FMC.Shared.DTOs;

public class TransactionDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Label { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string Category { get; set; } = string.Empty;
}
