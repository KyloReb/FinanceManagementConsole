using System.Security.Claims;
using FMC.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace FMC.Infrastructure.Services;

/// <summary>
/// Implementation of ICurrentUserService using IHttpContextAccessor to retrieve user claims.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Returns the "sub" (subject) claim which holds the user's primary identity ID.
    /// </summary>
    public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// In this single-tenant-per-user model, the UserId doubles as the TenantId.
    /// This can be expanded later to support organization-based TenantIds.
    /// </summary>
    public string? TenantId => UserId;

    /// <summary>
    /// True if the HTTP Context has an authenticated User identity.
    /// </summary>
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
