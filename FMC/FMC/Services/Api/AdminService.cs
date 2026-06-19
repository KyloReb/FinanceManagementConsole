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
        return await _httpClient.GetFromJsonAsync<List<UserDto>>($"api/users?_t={DateTime.UtcNow.Ticks}") ?? new();
    }

    public async Task<UserDto?> GetUserAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<UserDto>($"api/users/{id}?_t={DateTime.UtcNow.Ticks}");
    }

    public async Task<List<TransactionDto>> GetUserTransactionsAsync(string id, int count = 10)
    {
        return await _httpClient.GetFromJsonAsync<List<TransactionDto>>($"api/users/{id}/transactions?count={count}&_t={DateTime.UtcNow.Ticks}") ?? new();
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
        return await _httpClient.GetFromJsonAsync<List<FMC.Shared.DTOs.Admin.AuditLogDto>>($"api/audit/auth-logs?_t={DateTime.UtcNow.Ticks}") ?? new();
    }

    public async Task<List<FMC.Shared.DTOs.Admin.DocumentationDto>> GetDocsListAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<FMC.Shared.DTOs.Admin.DocumentationDto>>($"api/documentation/list?_t={DateTime.UtcNow.Ticks}") ?? new();
    }

    public async Task<FMC.Shared.DTOs.Admin.DocumentationDto?> GetDocAsync(string fileName)
    {
        return await _httpClient.GetFromJsonAsync<FMC.Shared.DTOs.Admin.DocumentationDto>($"api/documentation/{fileName}?_t={DateTime.UtcNow.Ticks}");
    }

    public async Task<FMC.Shared.DTOs.Admin.SystemHealthDto?> GetSystemHealthPulseAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<FMC.Shared.DTOs.Admin.SystemHealthDto>($"api/system/health-pulse?_t={DateTime.UtcNow.Ticks}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<FMC.Shared.DTOs.Admin.AuditLogDto>> GetRecentClientErrorsAsync(int count = 10)
    {
        try
        {
            var query = new FMC.Shared.DTOs.Admin.AuditLogQueryDto
            {
                Action = "CLIENT_CRASH",
                PageSize = count,
                Page = 1
            };
            var response = await _httpClient.PostAsJsonAsync("api/audit/search", query);
            if (!response.IsSuccessStatusCode) return new();
            var result = await response.Content.ReadFromJsonAsync<FMC.Shared.DTOs.Admin.AuditLogSearchResultDto>();
            return result?.Items ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<List<FMC.Shared.DTOs.Admin.MaintenanceHistoryItem>> GetMaintenanceHistoryAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<FMC.Shared.DTOs.Admin.MaintenanceHistoryItem>>($"api/system/maintenance/history?_t={DateTime.UtcNow.Ticks}") ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<FMC.Shared.DTOs.Admin.MaintenanceStatusDto?> GetMaintenanceStatusAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<FMC.Shared.DTOs.Admin.MaintenanceStatusDto>($"api/system/maintenance?_t={DateTime.UtcNow.Ticks}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Succeeded, string? Error)> ToggleMaintenanceModeAsync(bool isActive, string? message, DateTime? scheduledAt = null, string? scheduledMessage = null, string? modeType = null, int graceMinutes = 0)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/system/maintenance", new FMC.Shared.DTOs.Admin.MaintenanceToggleRequest
            {
                IsActive = isActive,
                Message = message,
                ScheduledAt = scheduledAt,
                ScheduledMessage = scheduledMessage,
                ModeType = modeType,
                GraceMinutes = graceMinutes
            });
            if (response.IsSuccessStatusCode) return (true, null);
            var error = await response.Content.ReadAsStringAsync();
            return (false, string.IsNullOrWhiteSpace(error) ? "Failed to update maintenance mode." : error);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
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
