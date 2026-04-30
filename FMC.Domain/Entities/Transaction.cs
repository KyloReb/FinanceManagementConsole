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

    /// <summary>
    /// The operational status of the transaction (e.g. Pending, Approved, Rejected).
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// The ID of the user who initiated this transaction (The Maker).
    /// </summary>
    public string? MakerId { get; set; }

    /// <summary>
    /// The ID of the user who approved or rejected this transaction (The Approver).
    /// </summary>
    public string? ApproverId { get; set; }

    /// <summary>
    /// The timestamp of the approval or rejection.
    /// </summary>
    public DateTime? ActionDate { get; set; }

    /// <summary>
    /// Feedback provided by the Approver if the transaction was rejected.
    /// </summary>
    public string? RejectionReason { get; set; }

    /// <summary>
    /// The organization context for this transaction.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>
    /// Link to a bulk upload batch, allowing grouped approvals.
    /// </summary>
    public Guid? BatchId { get; set; }
}
