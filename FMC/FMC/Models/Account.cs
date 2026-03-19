using System.ComponentModel.DataAnnotations;

namespace FMC.Models;

/// <summary>
/// Represents a financial account (e.g., Checking, Savings, Credit Card).
/// </summary>
public class Account
{
    /// <summary>
    /// Unique identifier for the account.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// The display name of the account.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The current available balance in the account.
    /// </summary>
    public decimal Balance { get; set; }
}
