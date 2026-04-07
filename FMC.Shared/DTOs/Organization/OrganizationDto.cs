namespace FMC.Shared.DTOs.Organization;

/// <summary>
/// Read-only projection of an Organization entity returned to consumers.
/// Only exposes fields safe for public transmission — no internal footprints.
/// </summary>
public class OrganizationDto
{
    /// <summary>The unique identifier of the organization.</summary>
    public Guid Id { get; set; }

    /// <summary>The canonical name of the organization.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description of the organization's business.</summary>
    public string? Description { get; set; }

    /// <summary>Whether the organization is currently operational.</summary>
    public bool IsActive { get; set; }

    /// <summary>UTC timestamp of when the organization was registered.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of when the organization was last modified.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Total balance summed from all accounts belonging to this organization.
    /// </summary>
    public decimal TotalBalance { get; set; }

    /// <summary>Total number of users affiliated with this organization.</summary>
    public int UserCount { get; set; }

    /// <summary>The name of the user representing the CEO for the organization, if any.</summary>
    public string? CeoName { get; set; }

    /// <summary>The unique ID of the Chief Executive user.</summary>
    public string? ChiefExecutiveId { get; set; }
}

/// <summary>
/// Mutation payload for registering a new Organization.
/// Intentionally excludes audit footprints (CreatedAt, IsDeleted) to prevent parameter pollution.
/// </summary>
public class CreateOrganizationDto
{
    /// <summary>The canonical name for the new organization. Must be unique across the system.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional business description for context.</summary>
    public string? Description { get; set; }
}

/// <summary>
/// Mutation payload for modifying an existing Organization record.
/// Intentionally excludes audit footprints to prevent parameter pollution attacks.
/// </summary>
public class UpdateOrganizationDto
{
    /// <summary>The UUID of the organization to be modified. Must match an existing record.</summary>
    public Guid Id { get; set; }

    /// <summary>The revised canonical name for the organization.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The revised description for the organization.</summary>
    public string? Description { get; set; }

    /// <summary>The UUID of the user to be assigned as Chief Executive.</summary>
    public string? ChiefExecutiveId { get; set; }

    /// <summary>Whether the organization should be marked as active or suspended.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// High-level financial KPIs for a specific organization, used in the CEO Dashboard.
/// </summary>
public class OrganizationDashboardMetricsDto
{
    /// <summary>Total corporate liquidity (sum of all user wallets).</summary>
    public decimal TotalBalance { get; set; }

    /// <summary>Total number of affiliated workforce members.</summary>
    public int UserCount { get; set; }

    /// <summary>Total settlement volume in the last 24 hours.</summary>
    public decimal DailyVolume { get; set; }
}
