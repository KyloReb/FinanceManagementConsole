using FMC.Data;
using FMC.Models;
using Microsoft.EntityFrameworkCore;

namespace FMC.Services;

/// <summary>
/// Defines the core financial data operations for the application.
/// </summary>
public interface IFinanceService
{
    /// <summary>
    /// Calculates the total balance across all bank accounts.
    /// </summary>
    /// <returns>The sum of all account balances.</returns>
    Task<decimal> GetTotalBalanceAsync();

    /// <summary>
    /// Calculates total expenses for the current calendar month.
    /// </summary>
    /// <returns>The absolute sum of all negative transactions in the current month.</returns>
    Task<decimal> GetMonthlyExpensesAsync();

    /// <summary>
    /// Gets the total number of active budgets defined in the system.
    /// </summary>
    /// <returns>The count of budget records.</returns>
    Task<int> GetActiveBudgetsCountAsync();

    /// <summary>
    /// Retrieves a specified number of the most recent transactions.
    /// </summary>
    /// <param name="count">The number of transactions to retrieve.</param>
    /// <returns>A list of transactions ordered by date descending.</returns>
    Task<List<Transaction>> GetRecentTransactionsAsync(int count);

    /// <summary>
    /// Retrieves all transactions stored in the database.
    /// </summary>
    /// <returns>A comprehensive list of all transactions.</returns>
    Task<List<Transaction>> GetAllTransactionsAsync();

    /// <summary>
    /// Adds a new transaction to the database and persists changes.
    /// </summary>
    /// <param name="transaction">The transaction entity to add.</param>
    Task AddTransactionAsync(Transaction transaction);

    /// <summary>
    /// Removes a transaction from the database by its unique identifier.
    /// </summary>
    /// <param name="id">The GUID of the transaction to delete.</param>
    Task DeleteTransactionAsync(Guid id);
}

/// <summary>
/// Implementation of <see cref="IFinanceService"/> using Entity Framework Core.
/// Provides high-level methods for business logic and data persistence.
/// </summary>
/// <param name="dbContext">The database context instance.</param>
public class FinanceService : IFinanceService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public FinanceService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }
    /// <inheritdoc/>
    public async Task<decimal> GetTotalBalanceAsync()
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.Accounts.SumAsync(a => a.Balance);
    }

    /// <inheritdoc/>
    public async Task<decimal> GetMonthlyExpensesAsync()
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        return await dbContext.Transactions
            .Where(t => t.Date >= startOfMonth && t.Amount < 0)
            .SumAsync(t => Math.Abs(t.Amount));
    }

    /// <inheritdoc/>
    public async Task<int> GetActiveBudgetsCountAsync()
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.Budgets.CountAsync();
    }

    /// <inheritdoc/>
    public async Task<List<Transaction>> GetRecentTransactionsAsync(int count)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.Transactions
            .OrderByDescending(t => t.Date)
            .Take(count)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<List<Transaction>> GetAllTransactionsAsync()
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.Transactions
            .OrderByDescending(t => t.Date)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task AddTransactionAsync(Transaction transaction)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task DeleteTransactionAsync(Guid id)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var transaction = await dbContext.Transactions.FindAsync(id);
        if (transaction != null)
        {
            dbContext.Transactions.Remove(transaction);
            await dbContext.SaveChangesAsync();
        }
    }
}
