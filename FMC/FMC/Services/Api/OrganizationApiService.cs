using System.Net.Http.Json;
using FMC.Shared.DTOs.Organization;
using FMC.Shared.DTOs.User;

namespace FMC.Services.Api;

/// <summary>
/// Client-side service for interacting with the Organizations API.
/// This service provides an abstracted wrapper over standard HTTP calls.
/// </summary>
public class OrganizationApiService
{
    private readonly HttpClient _httpClient;

    public OrganizationApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Retrieves a list of all active organizations from the API.
    /// </summary>
    public async Task<List<OrganizationDto>> GetOrganizationsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<OrganizationDto>>("api/organizations") ?? new();
    }

    /// <summary>
    /// Alias for GetOrganizationsAsync used by legacy components.
    /// </summary>
    public Task<List<OrganizationDto>> GetAllAsync() => GetOrganizationsAsync();

    /// <summary>
    /// Retrieves a single organization by its UUID.
    /// </summary>
    public async Task<OrganizationDto?> GetByIdAsync(Guid id)
    {
        var response = await _httpClient.GetAsync($"api/organizations/{id}");
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<OrganizationDto>();
    }

    /// <summary>
    /// Requests the registration of a new organization.
    /// </summary>
    public async Task<bool> CreateAsync(CreateOrganizationDto request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/organizations", request);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Submits modifications for an existing organization record.
    /// </summary>
    public async Task<bool> UpdateAsync(UpdateOrganizationDto request)
    {
        var response = await _httpClient.PutAsJsonAsync("api/organizations", request);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Requests the logical soft-deletion of an organization.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/organizations/{id}");
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Retrieves all users affiliated with the specified organization.
    /// </summary>
    public async Task<List<FMC.Shared.DTOs.User.UserDto>> GetUsersAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<List<FMC.Shared.DTOs.User.UserDto>>($"api/organizations/{id}/users") ?? new();
    }

    public async Task<bool> AdjustBalanceAsync(Guid id, decimal amount, string label)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/organizations/{id}/adjust-balance", new { Amount = amount, Label = label });
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Retrieves a unified list of system or financial audit events.
    /// </summary>
    public async Task<List<FMC.Shared.DTOs.Admin.AuditLogDto>> GetRecentAuditLogsAsync(int count = 20, string? category = null)
    {
        string url = $"api/audit/logs?count={count}";
        if (!string.IsNullOrEmpty(category)) url += $"&category={category}";
        
        return await _httpClient.GetFromJsonAsync<List<FMC.Shared.DTOs.Admin.AuditLogDto>>(url) ?? new();
    }

    public async Task<FMC.Shared.DTOs.Admin.AuditLogSearchResultDto> SearchAuditLogsAsync(FMC.Shared.DTOs.Admin.AuditLogQueryDto query)
    {
        var response = await _httpClient.PostAsJsonAsync("api/audit/search", query);
        return await response.Content.ReadFromJsonAsync<FMC.Shared.DTOs.Admin.AuditLogSearchResultDto>() ?? new();
    }

    public async Task<List<FMC.Shared.DTOs.Admin.SystemAlertDto>> GetActiveAlertsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<FMC.Shared.DTOs.Admin.SystemAlertDto>>("api/alerts/active") ?? new();
    }

    public async Task<int> GetUnresolvedAlertCountAsync()
    {
        return await _httpClient.GetFromJsonAsync<int>("api/alerts/count");
    }

    public async Task<bool> ResolveAlertAsync(long id)
    {
        var response = await _httpClient.PostAsync($"api/alerts/{id}/resolve", null);
        return response.IsSuccessStatusCode;
    }
}
