using FMC.Shared.DTOs.Organization;

namespace FMC.Application.Interfaces;

/// <summary>
/// Database-agnostic service abstraction for all Organization business operations.
/// All implementations are injected via DI — swap the concrete class freely without touching call sites.
/// </summary>
public interface IOrganizationService
{
    /// <summary>
    /// Retrieves all active (non-soft-deleted) organizations visible within the current tenant context.
    /// Results are ordered alphabetically by name for consistent UI rendering.
    /// </summary>
    Task<IEnumerable<OrganizationDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single organization by its unique UUID.
    /// Returns null if the record does not exist or has been soft-deleted.
    /// </summary>
    /// <param name="id">The UUID of the target organization.</param>
    Task<OrganizationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new organization into the system.
    /// Enforces uniqueness by rejecting duplicate names within the same tenant scope.
    /// </summary>
    /// <param name="dto">The creation payload containing name and optional description.</param>
    /// <returns>The fully hydrated DTO of the newly created organization.</returns>
    /// <exception cref="InvalidOperationException">Thrown if an organization with the same name already exists.</exception>
    Task<OrganizationDto> CreateAsync(CreateOrganizationDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies field-level mutations to an existing organization record.
    /// Automatically stamps the UpdatedAt audit footprint.
    /// </summary>
    /// <param name="dto">The update payload containing the target Id and revised field values.</param>
    /// <returns>True if the update was committed successfully; false if the record was not found.</returns>
    Task<bool> UpdateAsync(UpdateOrganizationDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes an existing organization record.
    /// Returns false if no matching record is found.
    /// </summary>
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a unified user profile (Staff or Cardholder) by their unique ID.
    /// </summary>
    Task<FMC.Shared.DTOs.User.UserDto?> GetUserByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all users (Staff and Cardholders) across all organizations.
    /// </summary>
    Task<IEnumerable<FMC.Shared.DTOs.User.UserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all users affiliated with a specific organization.
    /// </summary>
    Task<IEnumerable<FMC.Shared.DTOs.User.UserDto>> GetUsersByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a balance adjustment (Debit/Credit) for a specific organization and logs the action.
    /// </summary>
    Task<bool> AdjustBalanceAsync(Guid organizationId, decimal amount, string label, string performedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// CEO/Admin Endpoint: Initiates a balance adjustment for an individual user's personal wallet (Maker Step).
    /// </summary>
    Task<bool> AdjustUserBalanceAsync(Guid userId, decimal amount, string label, string performedBy, string? idempotencyKey = null, Guid? parentTransactionId = null, bool isSettlement = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approver Endpoint: Commits a pending transaction to the ledger and updates balances.
    /// </summary>
    Task<bool> ApproveTransactionAsync(Guid transactionId, string approverId, bool publishEvent = true, bool skipAuditLog = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approver Endpoint: Rejects a pending transaction.
    /// </summary>
    Task<bool> RejectTransactionAsync(Guid transactionId, string approverId, string reason, bool skipAuditLog = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Approver Endpoint: Commits an entire batch of pending transactions with a single action.
    /// </summary>
    Task<bool> ApproveBatchAsync(Guid batchId, string approverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approver Endpoint: Rejects an entire batch of pending transactions.
    /// </summary>
    Task<bool> RejectBatchAsync(Guid batchId, string approverId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all transactions currently in 'Pending' status for a specific organization.
    /// </summary>
    Task<IEnumerable<FMC.Shared.DTOs.TransactionDto>> GetPendingTransactionsAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all transactions occurs today (local time) for a specific organization.
    /// </summary>
    Task<IEnumerable<FMC.Shared.DTOs.TransactionDto>> GetTodayTransactionsAsync(Guid organizationId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Maker/Approver Endpoint: Retrieves a historical record of transactions for an organization.
    /// </summary>
    Task<IEnumerable<FMC.Shared.DTOs.TransactionDto>> GetOrganizationTransactionsAsync(Guid organizationId, string? status = null, int count = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Maker Endpoint: Cancels a pending transaction that was initiated by the current user.
    /// </summary>
    Task<bool> CancelTransactionAsync(Guid transactionId, string makerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Maker Endpoint: Cancels an entire pending batch that was initiated by the current user.
    /// </summary>
    Task<bool> CancelBatchAsync(Guid batchId, string makerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches high-priority operation alerts for specific user roles (CEO, Maker, Approver).
    /// </summary>
    Task<IEnumerable<FMC.Shared.DTOs.Admin.SystemAlertDto>> GetWorkflowAlertsAsync(Guid organizationId, string userId, string role, CancellationToken cancellationToken = default);

    /// <summary>
    /// SuperAdmin Endpoint: Synchronizes the Organization's Wallet Limit to match its current total liquidity.
    /// This effectively "resets" the capacity to the current balance.
    /// </summary>
    Task<bool> SyncOrganizationLimitAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
