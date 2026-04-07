using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FMC.Domain.Entities;
using FMC.Domain.Common;
using FMC.Application.Interfaces;

namespace FMC.Infrastructure.Data;

/// <summary>
/// The primary database context for the application, handling Identity and financial entities.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class.
    /// </summary>
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUserService) 
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Gets or sets the collection of financial transactions.
    /// </summary>
    public DbSet<Transaction> Transactions { get; set; }

    /// <summary>
    /// Gets or sets the collection of bank accounts.
    /// </summary>
    public DbSet<Account> Accounts { get; set; }

    /// <summary>
    /// Gets or sets the collection of budget definitions.
    /// </summary>
    public DbSet<Budget> Budgets { get; set; }

    /// <summary>
    /// Gets or sets the collection of user OTP verifications.
    /// </summary>
    public DbSet<UserOtpVerification> UserOtpVerifications { get; set; }

    /// <summary>
    /// Gets or sets the collection of enterprise organizations mapping users to tenants.
    /// </summary>
    public DbSet<Organization> Organizations { get; set; }

    /// <summary>
    /// Gets or sets the collection of audit logs.
    /// </summary>
    public DbSet<AuditLog> AuditLogs { get; set; }

    /// <summary>
    /// Gets or sets the collection of system alerts.
    /// </summary>
    public DbSet<SystemAlert> SystemAlerts { get; set; }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (string.IsNullOrEmpty(entry.Entity.TenantId))
                    {
                        var tenantId = _currentUserService.TenantId;
                        
                        if (string.IsNullOrEmpty(tenantId))
                        {
                            // Login logs often occur during the anonymous authentication boundary
                            if (entry.Entity is AuditLog)
                            {
                                entry.Entity.TenantId = "SYSTEM";
                            }
                            else
                            {
                                throw new UnauthorizedAccessException("TenantId is required.");
                            }
                        }
                        else
                        {
                            entry.Entity.TenantId = tenantId;
                        }
                    }
                    break;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Global Query Filters for Tenancy and Soft Deletions
        builder.Entity<Transaction>().HasQueryFilter(t => _currentUserService.IsSuperAdmin || t.TenantId == _currentUserService.TenantId);
        builder.Entity<Account>().HasQueryFilter(a => _currentUserService.IsSuperAdmin || a.TenantId == _currentUserService.TenantId);
        builder.Entity<Budget>().HasQueryFilter(b => _currentUserService.IsSuperAdmin || b.TenantId == _currentUserService.TenantId);
        builder.Entity<AuditLog>().HasQueryFilter(a => _currentUserService.IsSuperAdmin || a.TenantId == _currentUserService.TenantId);
        builder.Entity<Organization>().HasQueryFilter(o => !o.IsDeleted);

        builder.Entity<UserOtpVerification>()
            .HasIndex(o => o.UserId);
        builder.Entity<UserOtpVerification>()
            .HasIndex(o => o.ExpiresAt);

        builder.Entity<AuditLog>()
            .HasIndex(a => a.UserId);
        builder.Entity<AuditLog>()
            .HasIndex(a => a.CreatedAt);

        // Map explicit decimal types to silence EF Core warnings and prevent truncation
        builder.Entity<Transaction>().Property(t => t.Amount).HasColumnType("decimal(18,2)");
        builder.Entity<Account>().Property(a => a.Balance).HasColumnType("decimal(18,2)");
        builder.Entity<Budget>().Property(b => b.Limit).HasColumnType("decimal(18,2)");
        builder.Entity<AuditLog>().Property(a => a.Amount).HasColumnType("decimal(18,2)");
    }
}
