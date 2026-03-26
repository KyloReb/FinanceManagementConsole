using System.Net.Http.Json;
using FMC.Shared.DTOs.User;

namespace FMC.Services.Api;

public class AdminService
{
    private readonly HttpClient _httpClient;

    public AdminService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<UserDto>> GetUsersAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<UserDto>>("api/users") ?? new();
    }

    public async Task<UserDto?> GetUserAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<UserDto>($"api/users/{id}");
    }

    public async Task<bool> CreateUserAsync(CreateUserDto request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/users", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateUserAsync(UpdateUserDto request)
    {
        var response = await _httpClient.PutAsJsonAsync("api/users", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteUserAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"api/users/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<FMC.Shared.DTOs.Admin.AuditLogDto>> GetLoginLogsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<FMC.Shared.DTOs.Admin.AuditLogDto>>("api/audit/login-logs") ?? new();
    }
}
