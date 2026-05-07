using FMC.Application.Interfaces;
using FMC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FMC.Infrastructure.Services;

public interface IReconciliationService
{
    Task ReconcileAllAccountsAsync(CancellationToken ct = default);
}

public class ReconciliationService : IReconciliationService
{
    private readonly ApplicationDbContext _context;
    private readonly ISystemAlertService _alertService;
    private readonly ILogger<ReconciliationService> _logger;

    public ReconciliationService(
        ApplicationDbContext context, 
        ISystemAlertService alertService,
        ILogger<ReconciliationService> logger)
    {
        _context = context;
        _alertService = alertService;
        _logger = logger;
    }

    public async Task ReconcileAllAccountsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[Reconciliation] Starting global ledger integrity check...");
        
        var accounts = await _context.Accounts.IgnoreQueryFilters().ToListAsync(ct);
        int discrepanciesFound = 0;

        foreach (var account in accounts)
        {
            decimal transactionSum;

            if (account.Name == "Core Operations Wallet")
            {
                // The organizational wallet is the source of all subscriber allotments.
                // Its balance = (Sum of direct adjustments to it) - (Sum of all approved allotments to users).
                var directAdjustments = await _context.Transactions
                    .IgnoreQueryFilters()
                    .Where(t => t.AccountId == account.Id && t.Status == "Completed") // AdjustBalanceAsync sets status to "Completed"
                    .SumAsync(t => t.Amount, ct);

                var allotmentsToUsers = await _context.Transactions
                    .IgnoreQueryFilters()
                    .Where(t => t.OrganizationId == account.OrganizationId && t.AccountId != account.Id && t.Status == "Approved")
                    .SumAsync(t => t.Amount, ct);

                transactionSum = directAdjustments - allotmentsToUsers;
            }
            else
            {
                // Subscriber wallets: Sum of all approved transactions linked to this account.
                transactionSum = await _context.Transactions
                    .IgnoreQueryFilters()
                    .Where(t => t.AccountId == account.Id && t.Status == "Approved")
                    .SumAsync(t => t.Amount, ct);
            }

            if (Math.Abs(account.Balance - transactionSum) > 0.001m)
            {
                discrepanciesFound++;
                var diff = account.Balance - transactionSum;
                
                _logger.LogCritical("[Reconciliation] DISCREPANCY DETECTED! Account {Id} ({Name}). DB Balance: {Bal}, Ledger Sum: {Sum}, Diff: {Diff}", 
                    account.Id, account.Name, account.Balance, transactionSum, diff);

                await _alertService.RaiseAlertAsync(
                    "Ledger Integrity Violation",
                    $"Wallet '{account.Name}' balance drift of {diff:C} detected.",
                    FMC.Domain.Entities.AlertSeverity.Security,
                    account.Id.ToString(),
                    "Account"
                );
            }
        }

        _logger.LogInformation("[Reconciliation] Integrity check complete. Scanned {Count} accounts. Discrepancies: {Found}", 
            accounts.Count, discrepanciesFound);
    }
}
