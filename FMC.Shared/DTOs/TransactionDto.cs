namespace FMC.Shared.DTOs;

public class TransactionDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Label { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Subscriber { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? MakerName { get; set; }
    public string? MakerId { get; set; }
    public string? AccountNumber { get; set; }
    public string? ApproverName { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? ActionDate { get; set; }
    public Guid? OrganizationId { get; set; }
}
