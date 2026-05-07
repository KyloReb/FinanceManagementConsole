namespace FMC.Application.Interfaces;

/// <summary>
/// Core financial engine responsible for atomic balance adjustments and transfer operations.
/// Ensures mathematical integrity across the institutional ledger.
/// </summary>
public interface ILedgerService
{
    /// <summary>
    /// Atomically credits an account's balance.
    /// </summary>
    Task CreditAsync(Guid accountId, decimal amount, string? idempotencyKey = null, Guid? parentTransactionId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically debits an account's balance. 
    /// Should throw exceptions if overdraft limits (if any) are exceeded.
    /// </summary>
    Task DebitAsync(Guid accountId, decimal amount, string? idempotencyKey = null, Guid? parentTransactionId = null, bool allowNegative = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a zero-sum transfer between two accounts within the same transaction scope.
    /// </summary>
    Task TransferAsync(Guid sourceAccountId, Guid destinationAccountId, decimal amount, string? idempotencyKey = null, Guid? parentTransactionId = null, bool allowNegative = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current operational balance of an account.
    /// </summary>
    Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken = default);
}
