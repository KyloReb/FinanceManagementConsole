using FMC.Domain.Entities;

namespace FMC.Application.Interfaces;

/// <summary>
/// Data access abstraction for managing Organizations globally in a database-agnostic format.
/// Supports Enterprise scaling decoupling the core logic from specific ORM technologies.
/// </summary>
public interface IOrganizationRepository
{
    /// <summary>
    /// Locates an organization uniquely by its UUID sequence safely supporting soft deletion avoidance dynamically.
    /// </summary>
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires all organizations registered safely evaluating active tenancy scaling logic transparently.
    /// </summary>
    Task<IEnumerable<Organization>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches a newly minted organization entity into the core storage sequence awaiting synchronous transactions.
    /// </summary>
    Task AddAsync(Organization organization, CancellationToken cancellationToken = default);

    /// <summary>
    /// Overwrites entity attributes safely preventing data overlap securely propagating timestamp updates natively.
    /// </summary>
    void Update(Organization organization);

    /// <summary>
    /// Logically tags the organizational structure as obsolete without physically destroying its footprint natively.
    /// </summary>
    void SoftDelete(Organization organization);

    /// <summary>
    /// Validates if an organization name is already in use by another active entity.
    /// </summary>
    Task<bool> IsNameTakenAsync(string name, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>
    /// Calculates the count of non-deleted cardholders associated with an organization.
    /// </summary>
    Task<int> GetUserCountAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Calculates the sum of balances across all cardholder accounts for an organization.
    /// </summary>
    Task<decimal> GetTotalUserBalanceAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the current balance of the organization's core operations wallet.
    /// </summary>
    Task<decimal> GetOrganizationBalanceAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Persists all tracked changes to the storage medium.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);

    // --- Extended Transaction Lookups ---
    Task<Transaction?> GetTransactionByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Transaction>> GetTransactionsByStatusAsync(Guid organizationId, string status, CancellationToken ct = default);
    Task<IEnumerable<Transaction>> GetTransactionsByDateAsync(Guid organizationId, DateTime fromDate, CancellationToken ct = default);
    Task<IEnumerable<Transaction>> GetOrganizationTransactionsAsync(Guid organizationId, string? status, int count, CancellationToken ct = default);
    Task AddTransactionAsync(Transaction transaction, CancellationToken ct = default);

    // --- Extended Account Lookups ---
    Task<Account?> GetAccountByIdAsync(Guid id, CancellationToken ct = default);
    Task<Account?> GetAccountByTenantIdAsync(string tenantId, CancellationToken ct = default);
    Task AddAccountAsync(Account account, CancellationToken ct = default);
}
