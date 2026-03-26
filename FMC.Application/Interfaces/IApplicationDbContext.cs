using FMC.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FMC.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Transaction> Transactions { get; }
    DbSet<Account> Accounts { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<UserOtpVerification> UserOtpVerifications { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
