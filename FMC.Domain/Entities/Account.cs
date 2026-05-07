using System.ComponentModel.DataAnnotations;
using FMC.Domain.Common;

namespace FMC.Domain.Entities;

/// <summary>
/// Represents a financial account (e.g., Checking, Savings, Credit Card).
/// </summary>
public class Account : BaseEntity
{
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

    /// <summary>
    /// The organization that owns or manages this account.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>
    /// Byte array used for optimistic concurrency control.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
