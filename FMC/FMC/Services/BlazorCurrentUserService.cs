using System.Security.Claims;
using FMC.Application.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;

namespace FMC.Services;

/// <summary>
/// Implementation of ICurrentUserService for the Blazor Server/WASM hybrid environment.
/// This bridges the AuthenticationStateProvider claims to the common interface.
/// </summary>
public class BlazorCurrentUserService : ICurrentUserService
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public BlazorCurrentUserService(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    private ClaimsPrincipal? _user;

    private async Task EnsureUserLoaded()
    {
        if (_user == null)
        {
            var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
            _user = state.User;
        }
    }

    public string? UserId => GetClaim(ClaimTypes.NameIdentifier) ?? GetClaim("sub");
    public string? TenantId => GetClaim("TenantId") ?? UserId;
    public Guid? OrganizationId => Guid.TryParse(GetClaim("OrganizationId"), out var guid) ? guid : null;
    
    public bool IsAuthenticated => GetUser()?.Identity?.IsAuthenticated ?? false;
    public bool IsSuperAdmin => GetUser()?.IsInRole("SuperAdmin") ?? false;
    public bool IsCeo => GetUser()?.IsInRole("CEO") ?? false;
    public bool IsMaker => GetUser()?.IsInRole("Maker") ?? false;
    public bool IsApprover => GetUser()?.IsInRole("Approver") ?? false;

    private ClaimsPrincipal? GetUser()
    {
        var task = _authenticationStateProvider.GetAuthenticationStateAsync();
        if (task.IsCompletedSuccessfully)
        {
            return task.Result.User;
        }
        return null;
    }

    private string? GetClaim(string type)
    {
        return GetUser()?.FindFirstValue(type);
    }
}
