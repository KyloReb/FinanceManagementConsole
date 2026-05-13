using System.Net.Http.Json;
using FMC.Shared.DTOs.User;
using FMC.Shared.DTOs;

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

    public async Task<List<TransactionDto>> GetUserTransactionsAsync(string id, int count = 10)
    {
        return await _httpClient.GetFromJsonAsync<List<TransactionDto>>($"api/users/{id}/transactions?count={count}") ?? new();
    }

    public async Task<(bool Succeeded, string? Error)> CreateUserAsync(CreateUserDto request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/users", request);
        if (response.IsSuccessStatusCode) return (true, null);
        
        var error = await response.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(error) ? "Failed to create user." : error);
    }

    public async Task<(bool Succeeded, string? Error)> UpdateUserAsync(UpdateUserDto request)
    {
        var response = await _httpClient.PutAsJsonAsync("api/users", request);
        if (response.IsSuccessStatusCode) return (true, null);

        var error = await response.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(error) ? "Failed to update user." : error);
    }

    public async Task<bool> DeleteUserAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"api/users/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<FMC.Shared.DTOs.Admin.AuditLogDto>> GetAuthLogsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<FMC.Shared.DTOs.Admin.AuditLogDto>>("api/audit/auth-logs") ?? new();
    }

    public async Task<List<FMC.Shared.DTOs.Admin.DocumentationDto>> GetDocsListAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<FMC.Shared.DTOs.Admin.DocumentationDto>>("api/documentation/list") ?? new();
    }

    public async Task<FMC.Shared.DTOs.Admin.DocumentationDto?> GetDocAsync(string fileName)
    {
        return await _httpClient.GetFromJsonAsync<FMC.Shared.DTOs.Admin.DocumentationDto>($"api/documentation/{fileName}");
    }

    public async Task<FMC.Shared.DTOs.Admin.SystemHealthDto?> GetSystemHealthPulseAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<FMC.Shared.DTOs.Admin.SystemHealthDto>("api/system/health-pulse");
        }
        catch
        {
            return null;
        }
    }

    public async Task ReportClientErrorAsync(string message, string stackTrace, string? component = null)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("api/system/report-error", new 
            { 
                Message = message, 
                StackTrace = stackTrace, 
                Component = component,
                Timestamp = DateTime.UtcNow
            });
        }
        catch
        {
            // Silent fail for error reporting to avoid infinite loops
        }
    }
}
