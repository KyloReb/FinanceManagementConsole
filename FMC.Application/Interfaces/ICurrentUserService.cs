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
    /// Returns true if a user is currently authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
}
