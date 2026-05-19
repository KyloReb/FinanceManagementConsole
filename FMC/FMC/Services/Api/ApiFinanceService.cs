using System.Net.Http.Json;
using FMC.Shared.DTOs;

namespace FMC.Services.Api;

/// <summary>
/// Client-side service for handling financial requests to the API.
/// </summary>
public class ApiFinanceService
{
    private readonly HttpClient _httpClient;

    public ApiFinanceService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<decimal> GetTotalBalanceAsync()
    {
        return await _httpClient.GetFromJsonAsync<decimal>($"api/accounts/balance/total?_t={DateTime.UtcNow.Ticks}");
    }

    public async Task<decimal> GetMonthlyExpensesAsync()
    {
        return await _httpClient.GetFromJsonAsync<decimal>($"api/transactions/expenses/monthly?_t={DateTime.UtcNow.Ticks}");
    }

    public async Task<List<TransactionDto>> GetRecentTransactionsAsync(int count)
    {
        return await _httpClient.GetFromJsonAsync<List<TransactionDto>>($"api/transactions/recent?count={count}&_t={DateTime.UtcNow.Ticks}") ?? new();
    }

    public async Task<List<AccountDto>> GetAccountsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<AccountDto>>($"api/accounts?_t={DateTime.UtcNow.Ticks}") ?? new();
    }

    public async Task AddTransactionAsync(TransactionDto transaction)
    {
        await _httpClient.PostAsJsonAsync("api/transactions", transaction);
    }
}
