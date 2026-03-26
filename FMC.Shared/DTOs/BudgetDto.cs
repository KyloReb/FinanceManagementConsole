namespace FMC.Shared.DTOs;

public class BudgetDto
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Limit { get; set; }
    public string Period { get; set; } = "Monthly";
}
