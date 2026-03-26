using Microsoft.EntityFrameworkCore;
using FMC.Domain.Entities;

namespace FMC.Infrastructure.Data;

/// <summary>
/// Contains logic for initializing and seeding the database with initial data.
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// Seeds the database with default accounts, budgets, and transactions if it's currently empty.
    /// </summary>
    /// <param name="context">The database context to seed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task SeedData(ApplicationDbContext context)
    {
        if (await context.Accounts.AnyAsync()) return;

        var mainAccount = new Account { Name = "Checking", Balance = 5420.50m };
        var savingsAccount = new Account { Name = "Savings", Balance = 12500.00m };
        
        context.Accounts.AddRange(mainAccount, savingsAccount);

        var budgets = new List<Budget>
        {
            new Budget { Category = "Groceries", Limit = 500m, Period = "Monthly" },
            new Budget { Category = "Rent", Limit = 1200m, Period = "Monthly" },
            new Budget { Category = "Entertainment", Limit = 200m, Period = "Monthly" }
        };
        
        context.Budgets.AddRange(budgets);

        var transactions = new List<Transaction>
        {
            new Transaction { Date = DateTime.Now.AddDays(-2), Amount = -45.50m, Label = "Whole Foods", Category = "Groceries", AccountId = mainAccount.Id },
            new Transaction { Date = DateTime.Now.AddDays(-5), Amount = -1200.00m, Label = "Monthly Rent", Category = "Rent", AccountId = mainAccount.Id },
            new Transaction { Date = DateTime.Now.AddDays(-1), Amount = -30.00m, Label = "Netflix", Category = "Entertainment", AccountId = mainAccount.Id },
            new Transaction { Date = DateTime.Now.AddDays(-10), Amount = 3000.00m, Label = "Salary", Category = "Income", AccountId = mainAccount.Id },
            new Transaction { Date = DateTime.Now.AddDays(-3), Amount = -15.00m, Label = "Starbucks", Category = "Food", AccountId = mainAccount.Id }
        };
 
        context.Transactions.AddRange(transactions);
        await context.SaveChangesAsync();
    }
}
