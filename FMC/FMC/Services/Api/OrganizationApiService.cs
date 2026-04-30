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

    public event Action? OnDataChanged;
    public void NotifyDataChanged() => OnDataChanged?.Invoke();

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
    /// CEO Endpoint: Adjusts the balance of an individual user within the CEO's organization.
    /// </summary>
    public async Task<bool> AdjustUserBalanceAsync(Guid userId, decimal amount, string label)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/users/{userId}/adjust-balance", new { Amount = amount, Label = label });
        if (response.IsSuccessStatusCode) NotifyDataChanged();
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// CEO Endpoint: Retrieves high-level financial metrics for the CEO's affiliated organization.
    /// </summary>
    public async Task<OrganizationDashboardMetricsDto?> GetOrganizationMetricsAsync(Guid organizationId)
    {
        var response = await _httpClient.GetAsync($"api/organizations/{organizationId}/dashboard-metrics");
        if (!response.IsSuccessStatusCode) return null;
        
        return await response.Content.ReadFromJsonAsync<OrganizationDashboardMetricsDto>();
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

    public async Task<List<FMC.Shared.DTOs.Admin.SystemAlertDto>> GetWorkflowAlertsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<FMC.Shared.DTOs.Admin.SystemAlertDto>>("api/users/workflow-alerts") ?? new();
    }

    /// <summary>
    /// Approver Endpoint: Commits a pending transaction.
    /// </summary>
    public async Task<bool> ApproveTransactionAsync(Guid transactionId)
    {
        var response = await _httpClient.PostAsync($"api/users/transactions/{transactionId}/approve", null);
        if (!response.IsSuccessStatusCode)
        {
            var msg = await response.Content.ReadAsStringAsync();
            throw new Exception(!string.IsNullOrEmpty(msg) ? msg : "Approval failed.");
        }
        NotifyDataChanged();
        return true;
    }

    /// <summary>
    /// Approver Endpoint: Rejects a pending transaction with reason.
    /// </summary>
    public async Task<bool> RejectTransactionAsync(Guid transactionId, string reason)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/users/transactions/{transactionId}/reject", new { Reason = reason });
        if (!response.IsSuccessStatusCode)
        {
            var msg = await response.Content.ReadAsStringAsync();
            throw new Exception(!string.IsNullOrEmpty(msg) ? msg : "Rejection failed.");
        }
        NotifyDataChanged();
        return true;
    }

    /// <summary>
    /// Approver Endpoint: Commits an entire batch of pending transactions.
    /// </summary>
    public async Task<bool> ApproveBatchAsync(Guid batchId)
    {
        var response = await _httpClient.PostAsync($"api/users/transactions/batch/{batchId}/approve", null);
        if (!response.IsSuccessStatusCode)
        {
            var msg = await response.Content.ReadAsStringAsync();
            throw new Exception(!string.IsNullOrEmpty(msg) ? msg : "Batch approval failed.");
        }
        NotifyDataChanged();
        return true;
    }

    /// <summary>
    /// Approver Endpoint: Rejects an entire batch of pending transactions.
    /// </summary>
    public async Task<bool> RejectBatchAsync(Guid batchId, string reason)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/users/transactions/batch/{batchId}/reject", new { Reason = reason });
        if (!response.IsSuccessStatusCode)
        {
            var msg = await response.Content.ReadAsStringAsync();
            throw new Exception(!string.IsNullOrEmpty(msg) ? msg : "Batch rejection failed.");
        }
        NotifyDataChanged();
        return true;
    }

    /// <summary>
    /// Retrieves the list of transactions awaiting approval for an organization.
    /// </summary>
    public async Task<List<FMC.Shared.DTOs.TransactionDto>> GetOrganizationTransactionsAsync(Guid orgId, string? status = null, int count = 50)
    {
        string url = $"api/users/organizations/{orgId}/transactions?count={count}";
        if (!string.IsNullOrEmpty(status)) url += $"&status={status}";
        
        return await _httpClient.GetFromJsonAsync<List<FMC.Shared.DTOs.TransactionDto>>(url) ?? new();
    }

    public async Task<List<FMC.Shared.DTOs.TransactionDto>> GetPendingTransactionsAsync(Guid orgId)
    {
        return await _httpClient.GetFromJsonAsync<List<FMC.Shared.DTOs.TransactionDto>>($"api/users/organizations/{orgId}/pending-transactions") ?? new();
    }

    public async Task<List<FMC.Shared.DTOs.TransactionDto>> GetTodayTransactionsAsync(Guid orgId)
    {
        return await _httpClient.GetFromJsonAsync<List<FMC.Shared.DTOs.TransactionDto>>($"api/users/organizations/{orgId}/today-transactions") ?? new();
    }

    /// <summary>
    /// Maker Endpoint: Cancels a pending transaction initiated by the current user.
    /// </summary>
    public async Task<bool> CancelTransactionAsync(Guid transactionId)
    {
        var response = await _httpClient.DeleteAsync($"api/users/transactions/{transactionId}/cancel");
        if (response.IsSuccessStatusCode) NotifyDataChanged();
        return response.IsSuccessStatusCode;
    }

    public async Task<List<FMC.Shared.DTOs.BulkTransactionRowDto>> ParseExcelCardholderFileAsync(Guid orgId, Stream fileStream)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(streamContent, "file", "upload.xlsx");

        var response = await _httpClient.PostAsync($"api/excel/parse-cardholders/{orgId}", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<FMC.Shared.DTOs.BulkTransactionRowDto>>() ?? new();
    }

    public async Task<FMC.Shared.DTOs.BulkUploadResultDto> SubmitBulkTransactionAsync(Guid orgId, bool isCredit, List<FMC.Shared.DTOs.BulkTransactionRowDto> rows)
    {
        var payload = new FMC.Shared.DTOs.BulkTransactionRequestDto { IsCredit = isCredit, Rows = rows };
        var response = await _httpClient.PostAsJsonAsync($"api/organizations/{orgId}/transactions/bulk", payload);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FMC.Shared.DTOs.BulkUploadResultDto>() ?? new();
    }

    public async Task<List<FMC.Shared.DTOs.BulkTransactionRowDto>> ValidateRowsAsync(Guid orgId, List<FMC.Shared.DTOs.BulkTransactionRowDto> rows)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/excel/validate-rows/{orgId}", rows);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<FMC.Shared.DTOs.BulkTransactionRowDto>>() ?? new();
    }
}
