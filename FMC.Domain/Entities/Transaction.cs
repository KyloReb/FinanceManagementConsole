using System.ComponentModel.DataAnnotations;
using FMC.Domain.Common;

namespace FMC.Domain.Entities;

/// <summary>
/// Represents a single financial transaction (income or expense).
/// </summary>
public class Transaction : BaseEntity
{
    /// <summary>
    /// The date and time the transaction occurred.
    /// </summary>
    [Required]
    public DateTime Date { get; set; } = DateTime.Now;
    
    /// <summary>
    /// The monetary value of the transaction. Negative values indicate expenses.
    /// </summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }
    
    /// <summary>
    /// A short descriptive label for the transaction.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Label { get; set; } = string.Empty;
    
    /// <summary>
    /// The ID of the account associated with this transaction.
    /// </summary>
    public Guid AccountId { get; set; }
    
    /// <summary>
    /// The category classification for the transaction.
    /// </summary>
    [Required]
    public string Category { get; set; } = string.Empty;
}
