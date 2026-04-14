namespace FMC.Application.Interfaces;

/// <summary>
/// Service to retrieve context about the currently authenticated user and their tenant.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// The unique identifier of the user (e.g., from JWT "sub" claim).
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// The tenant identifier associated with the current session.
    /// In this implementation, UserId acts as the primary TenantId.
    /// </summary>
    string? TenantId { get; }
    
    /// <summary>
    /// The specific organization identifier if the user belongs to one.
    /// </summary>
    Guid? OrganizationId { get; }

    /// <summary>
    /// Returns true if a user is currently authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Returns true if the user acts as a global administrator (SuperAdmin).
    /// </summary>
    bool IsSuperAdmin { get; }
    
    /// <summary>
    /// Returns true if the user has the CEO role.
    /// </summary>
    bool IsCeo { get; }

    /// <summary>
    /// Returns true if the user has the Maker role for financial initiation.
    /// </summary>
    bool IsMaker { get; }

    /// <summary>
    /// Returns true if the user has the Approver role for financial verification.
    /// </summary>
    bool IsApprover { get; }
}
