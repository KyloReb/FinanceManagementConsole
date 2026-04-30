using FMC.Domain.Common;

namespace FMC.Domain.Entities;

/// <summary>
/// Represents a customer/subscriber holding a financial card.
/// Separated from the Identity system to allow for independent scaling and database residency.
/// </summary>
public class Cardholder : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// The unique 16-digit card/account number for this user, starting with 63641.
    /// This is the primary identifier for financial transactions.
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Foreign Key to the Organization this cardholder belongs to.
    /// </summary>
    public Guid OrganizationId { get; set; }
    public virtual Organization? Organization { get; set; }

    /// <summary>
    /// Corresponds to the TenantId for data isolation. In this case, 
    /// it maps to the OrganizationId or a specialized system ID.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Optional link to an Identity User if this cardholder is granted web/mobile access.
    /// This allows the core Cardholder record to exist even if the user never logs in.
    /// </summary>
    public string? IdentityUserId { get; set; }
}
