using FMC.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FMC.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Transaction> Transactions { get; }
    DbSet<Account> Accounts { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<UserOtpVerification> UserOtpVerifications { get; }
    DbSet<Organization> Organizations { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<SystemAlert> SystemAlerts { get; }
    DbSet<ApplicationUser> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
