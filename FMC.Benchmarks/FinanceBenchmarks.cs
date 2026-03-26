using Microsoft.EntityFrameworkCore;
using Moq;
using FMC.Application.Interfaces;
using FMC.Infrastructure.Data;
using FMC.Domain.Entities;

namespace FMC.Benchmarks;

[MemoryDiagnoser]
public class FinanceBenchmarks
{
    private FinanceService _service = default!;
    private ApplicationDbContext _dbContext = default!;

    [GlobalSetup]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "BenchmarkDb")
            .Options;

        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.TenantId).Returns("bench-tenant");

        _dbContext = new ApplicationDbContext(options, mockUser.Object);

        // Seed some data
        _dbContext.Accounts.Add(new Account { Id = Guid.NewGuid(), Name = "Test", Balance = 1000m });
        for (int i = 0; i < 100; i++)
        {
            _dbContext.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                Label = $"Transaction {i}",
                Amount = -10m,
                Date = DateTime.Now
            });
        }
        _dbContext.SaveChanges();

        var mockFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        mockFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ApplicationDbContext(options, mockUser.Object));

        _service = new FinanceService(mockFactory.Object);
    }

    [Benchmark]
    public async Task<decimal> GetTotalBalance()
    {
        return await _service.GetTotalBalanceAsync();
    }

    [Benchmark]
    public async Task<decimal> GetMonthlyExpenses()
    {
        return await _service.GetMonthlyExpensesAsync();
    }

    [Benchmark]
    public async Task<List<Transaction>> GetRecentTransactions()
    {
        return await _service.GetRecentTransactionsAsync(10);
    }
}
