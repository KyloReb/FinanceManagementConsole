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
    /// In this model, we first check for an Organization-level tenant context.
    /// If missing, it falls back to the individual User's ID.
    /// </summary>
    public string? TenantId 
    {
        get
        {
            var orgId = _httpContextAccessor.HttpContext?.User?.FindFirstValue("OrganizationId");
            return !string.IsNullOrEmpty(orgId) ? orgId : UserId;
        }
    }

    /// <summary>
    /// True if the HTTP Context has an authenticated User identity.
    /// </summary>
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    /// <summary>
    /// Evaluates if the current user has the SuperAdmin/System Admin role, bypassing standard tenancy.
    /// </summary>
    public bool IsSuperAdmin => _httpContextAccessor.HttpContext?.User?.IsInRole(FMC.Shared.Auth.Roles.SuperAdmin) == true;
}
