namespace FMC.Shared.DTOs;

public class CompensationRegisterRow
{
    public string Subscriber { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal TotalCredits { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal NetAmount => TotalCredits - TotalDebits;
    public int TransactionCount { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
}
