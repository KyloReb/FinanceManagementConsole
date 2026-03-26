using FMC.Data;
using FMC.Models;
using FMC.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using FMC.Application.Interfaces;
using FMC.Infrastructure.Data;

namespace FMC.Tests.Integration;

[Collection("SharedDatabase")]
public class FinanceServiceTests : IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private readonly ApplicationDbContext _dbContext;
    private readonly FinanceService _service;

    public FinanceServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_fixture.ConnectionString)
            .Options;

        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.TenantId).Returns("test-tenant");

        _dbContext = new ApplicationDbContext(options, mockUser.Object);

        // Mock the IDbContextFactory to return our container-based context
        var mockFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        mockFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ApplicationDbContext(options, mockUser.Object));

        _service = new FinanceService(mockFactory.Object);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.ResetDatabaseAsync();
    }

    [Fact]
    public async Task AddTransactionAsync_ShouldPersistToDatabase()
    {
        // Arrange
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Label = "Test Transaction",
            Category = "Food",
            Amount = -50.00m,
            Date = DateTime.Now
        };

        // Act
        await _service.AddTransactionAsync(transaction);

        // Assert
        var persisted = await _dbContext.Transactions.FindAsync(transaction.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Test Transaction", persisted.Label);
        Assert.Equal(-50.00m, persisted.Amount);
    }

    [Fact]
    public async Task GetTotalBalanceAsync_ShouldReturnSumOfAccountBalances()
    {
        // Arrange
        _dbContext.Accounts.AddRange(
            new Account { Id = Guid.NewGuid(), Name = "Bank A", Balance = 1000m },
            new Account { Id = Guid.NewGuid(), Name = "Bank B", Balance = 500m }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var totalBalance = await _service.GetTotalBalanceAsync();

        // Assert
        Assert.Equal(1500m, totalBalance);
    }
}
