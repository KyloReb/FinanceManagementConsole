using FMC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FMC.Infrastructure.Data;

/// <summary>
/// Ensures the database schema has all required columns and seed data.
/// Runs once per application lifetime (guarded by a static flag) to bridge
/// legacy schema gaps until proper EF Core migrations are introduced.
/// 
/// All ALTER TABLE statements are idempotent — they check for column existence
/// before attempting to add. Logs errors per-column so a single failure does
/// not block the entire initialization.
/// </summary>
public class DatabaseInitializer
{
    private static bool _initialized;

    private readonly ApplicationDbContext _db;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        ApplicationDbContext db,
        IServiceProvider serviceProvider,
        ILogger<DatabaseInitializer> logger)
    {
        _db = db;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Runs all schema synchronization and seeding steps exactly once
    /// per application lifetime.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            _logger.LogDebug("Database already initialized in this process lifetime. Skipping.");
            return;
        }

        _initialized = true;
        _logger.LogInformation("Running database initialization…");

        await SyncTenantIdColumnsAsync();
        await SyncOrganizationIdColumnsAsync();
        await SyncDeviceColumnAsync();
        await SyncAspNetUsersOrganizationAsync();
        await SeedRolesAndAdminAsync();
        await RepairLegacyIdsAsync();

        _logger.LogInformation("Database initialization completed.");
    }

    // ─────────────────────────────────────────────────────────────
    //  Schema synchronization helpers
    // ─────────────────────────────────────────────────────────────

    private async Task SyncTenantIdColumnsAsync()
    {
        var tables = new[] { "Accounts", "Transactions", "Budgets", "AuditLogs" };
        foreach (var table in tables)
        {
            try
            {
                var sql =
                    $"IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[{table}]') AND name = N'TenantId') " +
                    $"BEGIN ALTER TABLE [{table}] ADD [TenantId] nvarchar(max) NOT NULL DEFAULT N''; END";
                await _db.Database.ExecuteSqlRawAsync(sql);
                _logger.LogInformation("Ensured TenantId column on [{Table}]", table);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synchronizing TenantId for table [{Table}]", table);
            }
        }
    }

    private async Task SyncOrganizationIdColumnsAsync()
    {
        var tables = new[] { "Accounts", "Transactions" };
        foreach (var table in tables)
        {
            try
            {
                var sql =
                    $"IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[{table}]') AND name = N'OrganizationId') " +
                    $"BEGIN ALTER TABLE [{table}] ADD [OrganizationId] uniqueidentifier NULL; END";
                await _db.Database.ExecuteSqlRawAsync(sql);
                _logger.LogInformation("Ensured OrganizationId column on [{Table}]", table);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synchronizing OrganizationId for table [{Table}]", table);
            }
        }
    }

    private async Task SyncDeviceColumnAsync()
    {
        try
        {
            var sql =
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[AuditLogs]') AND name = N'Device') " +
                "BEGIN ALTER TABLE [AuditLogs] ADD [Device] nvarchar(max) NULL; END";
            await _db.Database.ExecuteSqlRawAsync(sql);
            _logger.LogInformation("Ensured Device column on [AuditLogs]");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing Device column for AuditLogs");
        }
    }

    private async Task SyncAspNetUsersOrganizationAsync()
    {
        try
        {
            var addColumnSql =
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[AspNetUsers]') AND name = N'Organization') " +
                "BEGIN ALTER TABLE [AspNetUsers] ADD [Organization] nvarchar(max) NULL; END";
            await _db.Database.ExecuteSqlRawAsync(addColumnSql);

            var updateSql =
                "UPDATE [AspNetUsers] SET [Organization] = N'Nationlink/Infoserve Inc.' WHERE [Organization] IS NULL";
            await _db.Database.ExecuteSqlRawAsync(updateSql);

            _logger.LogInformation("Ensured Organization column on [AspNetUsers] and set default value");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing Organization column for AspNetUsers");
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Seeding and data repair
    // ─────────────────────────────────────────────────────────────

    private async Task SeedRolesAndAdminAsync()
    {
        try
        {
            await ApplicationDbSeeder.SeedRolesAndAdminAsync(_serviceProvider);
            _logger.LogInformation("Database seeding completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while seeding roles and admin account.");
        }
    }

    private async Task RepairLegacyIdsAsync()
    {
        try
        {
            await Scripts.DataRepair.AlignLegacyIds(_db);
            _logger.LogInformation("Legacy ID alignment completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while aligning legacy IDs.");
        }
    }
}
