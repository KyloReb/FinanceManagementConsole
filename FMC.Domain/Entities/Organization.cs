using System.ComponentModel.DataAnnotations;
using FMC.Domain.Common;

namespace FMC.Domain.Entities;

/// <summary>
/// Represents a unique client organization or company affiliated with multiple users.
/// </summary>
public class Organization : BaseEntity
{
    /// <summary>
    /// The canonical name of the organization.
    /// </summary>
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Additional context or details regarding the organization's business.
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Indicates whether the organization is fully operational or suspended.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Audit footprint detailing when this record was originally registered in the ledger.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Audit footprint tracking the latest modification made to the record.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Enterprise Soft-Deletion flag. Set true to logically delete the record without losing referential historical data.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// The unique identifier of the user serving as the organization's Chief Executive/CEO.
    /// </summary>
    public string? ChiefExecutiveId { get; set; }

    /// <summary>
    /// Soft-Deletion chronological footprint.
    /// </summary>
    public DateTime? DeletedAt { get; set; }
}
