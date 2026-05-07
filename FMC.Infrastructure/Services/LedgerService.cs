using FMC.Application.Interfaces;
using FMC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FMC.Infrastructure.Services;

public class LedgerService : ILedgerService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LedgerService> _logger;

    public LedgerService(ApplicationDbContext context, ILogger<LedgerService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task CreditAsync(Guid accountId, decimal amount, string? idempotencyKey = null, Guid? parentTransactionId = null, CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            var account = await _context.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
            if (account == null) throw new ApplicationException($"Ledger Error: Account {accountId} not found for credit.");

            account.Balance += amount;
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("[Ledger] Credited {Amount} to Account {Id}. New Balance: {Balance}", amount, accountId, account.Balance);
        }, cancellationToken);
    }

    public async Task DebitAsync(Guid accountId, decimal amount, string? idempotencyKey = null, Guid? parentTransactionId = null, bool allowNegative = false, CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            var account = await _context.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
            if (account == null) throw new ApplicationException($"Ledger Error: Account {accountId} not found for debit.");

            if (!allowNegative && account.Balance < amount)
            {
                _logger.LogWarning("[Ledger] Insufficient funds for Debit on Account {Id}. Balance: {Bal}, Req: {Req}", accountId, account.Balance, amount);
                throw new InvalidOperationException("Insufficient funds in institutional wallet.");
            }

            account.Balance -= amount;
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("[Ledger] Debited {Amount} from Account {Id}. New Balance: {Balance}", amount, accountId, account.Balance);
        }, cancellationToken);
    }

    public async Task TransferAsync(Guid sourceAccountId, Guid destinationAccountId, decimal amount, string? idempotencyKey = null, Guid? parentTransactionId = null, bool allowNegative = false, CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            var source = await _context.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == sourceAccountId, cancellationToken);
            var dest = await _context.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == destinationAccountId, cancellationToken);

            if (source == null || dest == null)
                throw new ApplicationException("Ledger Error: One or both accounts missing for transfer.");

            if (!allowNegative && source.Balance < amount)
                throw new InvalidOperationException("Source account has insufficient operational liquidity.");

            source.Balance -= amount;
            dest.Balance += amount;

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("[Ledger] Transferred {Amount} from {Source} to {Dest}.", amount, sourceAccountId, destinationAccountId);
        }, cancellationToken);
    }

    private async Task ExecuteWithRetryAsync(Func<Task> action, CancellationToken ct)
    {
        int retries = 3;
        while (retries > 0)
        {
            try
            {
                await action();
                return;
            }
            catch (DbUpdateConcurrencyException)
            {
                retries--;
                if (retries == 0) throw;
                
                // Reset context to fetch fresh data on next iteration
                foreach (var entry in _context.ChangeTracker.Entries()) entry.State = EntityState.Detached;
                
                _logger.LogWarning("[Ledger] Concurrency conflict detected. Retrying... ({Retries} left)", retries);
                await Task.Delay(100, ct); 
            }
        }
    }

    public async Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await _context.Accounts.IgnoreQueryFilters()
            .Where(a => a.Id == accountId)
            .Select(a => a.Balance)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
