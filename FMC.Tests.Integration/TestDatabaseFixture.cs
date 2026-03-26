using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Respawn;
using System.Data.Common;
using FMC.Application.Interfaces;
using FMC.Infrastructure.Data;
using Moq;

namespace FMC.Tests.Integration;

public class TestDatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private DbConnection _connection = default!;
    private Respawner _respawner = default!;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.TenantId).Returns("test-tenant");

        using var dbContext = new ApplicationDbContext(options, mockUser.Object);
        await dbContext.Database.MigrateAsync();

        _connection = dbContext.Database.GetDbConnection();
        await _connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = new[] { "dbo" }
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await _respawner.ResetAsync(_connection);
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
    }
}

[CollectionDefinition("SharedDatabase")]
public class SharedDatabaseCollection : ICollectionFixture<TestDatabaseFixture>
{
}
