using System.ComponentModel.DataAnnotations;
using FMC.Domain.Common;

namespace FMC.Domain.Entities;

/// <summary>
/// Represents a spending limit for a specific category over a period of time.
/// </summary>
public class Budget : BaseEntity
{
    
    /// <summary>
    /// The category this budget applies to (e.g., Groceries, Rent).
    /// </summary>
    [Required]
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// The maximum allowed spending amount for the period.
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal Limit { get; set; }
    
    /// <summary>
    /// The time interval for the budget (e.g., "Monthly").
    /// </summary>
    [Required]
    public string Period { get; set; } = "Monthly"; // e.g., Monthly, Yearly
}
