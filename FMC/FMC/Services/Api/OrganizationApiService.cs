using System.Net.Http.Json;
using FMC.Shared.DTOs.Organization;

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
    public async Task<List<OrganizationDto>> GetAllAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<OrganizationDto>>("api/organizations") ?? new();
    }

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
}
