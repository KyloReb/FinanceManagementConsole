using FMC.Application.Interfaces;
using FMC.Domain.Entities;
using FMC.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;

namespace FMC.Infrastructure.Repositories;

/// <summary>
/// A resilience decorator that wraps the primary OrganizationRepository.
/// Applies Polly retry policies to all data access methods transparently,
/// so the calling service layer never needs to know about retry logic.
/// This follows the Decorator Pattern, keeping the base repository clean
/// and making the resilience layer independently testable and replaceable.
/// </summary>
public class ResilientOrganizationRepository : IOrganizationRepository
{
    private readonly IOrganizationRepository _inner;
    private readonly Polly.ResiliencePipeline _pipeline;

    public ResilientOrganizationRepository(
        OrganizationRepository inner,
        ILogger<ResilientOrganizationRepository> logger)
    {
        _inner = inner;
        _pipeline = ResiliencePolicies.GetDatabaseResiliencePipeline(logger);
    }

    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _pipeline.ExecuteAsync(async ct => await _inner.GetByIdAsync(id, ct), cancellationToken).AsTask();

    public Task<IEnumerable<Organization>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _pipeline.ExecuteAsync(async ct => await _inner.GetAllAsync(ct), cancellationToken).AsTask();

    public Task<IEnumerable<(Organization Org, int UserCount, decimal OrgBalance, decimal UserBalanceSum, string? CeoName)>> GetAllWithStatsAsync(CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetAllWithStatsAsync(token), ct).AsTask();

    public Task<bool> IsNameTakenAsync(string name, Guid? excludeId = null, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.IsNameTakenAsync(name, excludeId, token), ct).AsTask();

    public Task<int> GetUserCountAsync(Guid organizationId, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetUserCountAsync(organizationId, token), ct).AsTask();

    public Task<decimal> GetTotalUserBalanceAsync(Guid organizationId, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetTotalUserBalanceAsync(organizationId, token), ct).AsTask();

    public Task<decimal> GetOrganizationBalanceAsync(Guid organizationId, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetOrganizationBalanceAsync(organizationId, token), ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.SaveChangesAsync(token), ct).AsTask();

    public Task<Cardholder?> GetCardholderByIdAsync(Guid id, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetCardholderByIdAsync(id, token), ct).AsTask();

    public Task<Cardholder?> GetCardholderByAccountNumberAsync(string accountNumber, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetCardholderByAccountNumberAsync(accountNumber, token), ct).AsTask();

    public Task<Cardholder?> GetCardholderByAccountNumberAsync(string accountNumber, Guid organizationId, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetCardholderByAccountNumberAsync(accountNumber, organizationId, token), ct).AsTask();

    public Task<IEnumerable<Cardholder>> GetAllCardholdersAsync(CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetAllCardholdersAsync(token), ct).AsTask();

    public Task<IEnumerable<Cardholder>> GetCardholdersByOrganizationAsync(Guid organizationId, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetCardholdersByOrganizationAsync(organizationId, token), ct).AsTask();

    public Task<Transaction?> GetTransactionByIdAsync(Guid id, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetTransactionByIdAsync(id, token), ct).AsTask();

    public Task<IEnumerable<Transaction>> GetTransactionsByStatusAsync(Guid organizationId, string status, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetTransactionsByStatusAsync(organizationId, status, token), ct).AsTask();

    public Task<IEnumerable<Transaction>> GetTransactionsByDateAsync(Guid organizationId, DateTime fromDate, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetTransactionsByDateAsync(organizationId, fromDate, token), ct).AsTask();

    public Task<IEnumerable<Transaction>> GetProcessedTransactionsSinceAsync(Guid organizationId, DateTime since, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetProcessedTransactionsSinceAsync(organizationId, since, token), ct).AsTask();

    public Task<IEnumerable<Transaction>> GetOrganizationTransactionsAsync(Guid organizationId, string? status, int count, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetOrganizationTransactionsAsync(organizationId, status, count, token), ct).AsTask();

    public Task<IEnumerable<Transaction>> GetTransactionsByBatchIdAsync(Guid batchId, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetTransactionsByBatchIdAsync(batchId, token), ct).AsTask();

    public Task AddTransactionAsync(Transaction transaction, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.AddTransactionAsync(transaction, token), ct).AsTask();

    public Task<Account?> GetAccountByIdAsync(Guid id, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetAccountByIdAsync(id, token), ct).AsTask();

    public Task<Account?> GetAccountByTenantIdAsync(string tenantId, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetAccountByTenantIdAsync(tenantId, token), ct).AsTask();

    public Task<Account?> GetAccountByCardNumberAsync(string cardNumber, Guid organizationId, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.GetAccountByCardNumberAsync(cardNumber, organizationId, token), ct).AsTask();

    public Task AddAccountAsync(Account account, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.AddAccountAsync(account, token), ct).AsTask();

    public Task<bool> ExistsTransactionWithIdempotencyKeyAsync(string key, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async token => await _inner.ExistsTransactionWithIdempotencyKeyAsync(key, token), ct).AsTask();

    // Passthrough methods (no DB call, state mutation only)
    public void Update(Organization organization) => _inner.Update(organization);
    public void SoftDelete(Organization organization) => _inner.SoftDelete(organization);
    public Task AddAsync(Organization organization, CancellationToken cancellationToken = default) =>
        _pipeline.ExecuteAsync(async ct => await _inner.AddAsync(organization, ct), cancellationToken).AsTask();
}
