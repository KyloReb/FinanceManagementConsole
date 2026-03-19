using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FMC.Models;

namespace FMC.Data;

/// <summary>
/// The primary database context for the application, handling Identity and financial entities.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to be used by the DbContext.</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) 
        : base(options)
    {
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
    /// Gets or sets the collection of audit logs.
    /// </summary>
    public DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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
    }
}
