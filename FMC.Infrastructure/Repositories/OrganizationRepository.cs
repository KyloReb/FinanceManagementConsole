using Microsoft.EntityFrameworkCore;
using FMC.Application.Interfaces;
using FMC.Domain.Entities;
using FMC.Infrastructure.Data;

namespace FMC.Infrastructure.Repositories;

/// <summary>
/// Contains the specific Entity Framework dialect configurations answering the Interface contracts reliably dynamically scaling context filters.
/// </summary>
public class OrganizationRepository : IOrganizationRepository
{
    private readonly ApplicationDbContext _context;

    public OrganizationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Organization>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        await _context.Organizations.AddAsync(organization, cancellationToken);
    }

    public void Update(Organization organization)
    {
        organization.UpdatedAt = DateTime.UtcNow;
        _context.Organizations.Update(organization);
    }

    public void SoftDelete(Organization organization)
    {
        // Safe logical removal keeping history intact but hiding from the native contextual lookups
        organization.IsDeleted = true;
        organization.DeletedAt = DateTime.UtcNow;
        _context.Organizations.Update(organization);
    }

    public async Task<bool> IsNameTakenAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
    {
        return await _context.Organizations
            .AnyAsync(o => o.Name.ToLower() == name.ToLower() && (!excludeId.HasValue || o.Id != excludeId), ct);
    }

    public async Task<int> GetUserCountAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await _context.Users.OfType<ApplicationUser>()
            .CountAsync(u => u.OrganizationId == organizationId, ct);
    }

    public async Task<decimal> GetTotalUserBalanceAsync(Guid organizationId, CancellationToken ct = default)
    {
        var orgTenantId = organizationId.ToString();
        return await (from u in _context.Users.OfType<ApplicationUser>()
                      where u.OrganizationId == organizationId
                      join a in _context.Accounts.IgnoreQueryFilters() on u.Id equals a.TenantId
                      where a.TenantId != orgTenantId // Don't include the org's own operational wallet
                      select a.Balance).SumAsync(ct);
    }

    public async Task<decimal> GetOrganizationBalanceAsync(Guid organizationId, CancellationToken ct = default)
    {
        var orgTenantId = organizationId.ToString();
        return await _context.Accounts
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == orgTenantId)
            .Select(a => a.Balance)
            .FirstOrDefaultAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }

    public async Task<Transaction?> GetTransactionByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Transactions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByStatusAsync(Guid organizationId, string status, CancellationToken ct = default)
    {
        return await _context.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.OrganizationId == organizationId && t.Status == status)
            .OrderByDescending(t => t.Date)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByDateAsync(Guid organizationId, DateTime fromDate, CancellationToken ct = default)
    {
        return await _context.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.OrganizationId == organizationId && t.Date >= fromDate)
            .OrderByDescending(t => t.Date)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Transaction>> GetProcessedTransactionsSinceAsync(Guid organizationId, DateTime since, CancellationToken ct = default)
    {
        return await _context.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.OrganizationId == organizationId 
                    && (t.Status == "Approved" || t.Status == "Rejected" || t.Status == "Successful")
                    && t.ActionDate >= since)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Transaction>> GetOrganizationTransactionsAsync(Guid organizationId, string? status, int count, CancellationToken ct = default)
    {
        var query = _context.Transactions.IgnoreQueryFilters().Where(t => t.OrganizationId == organizationId);
        if (!string.IsNullOrEmpty(status)) query = query.Where(t => t.Status == status);
        
        return await query.OrderByDescending(t => t.Date).Take(count).ToListAsync(ct);
    }

    public async Task AddTransactionAsync(Transaction transaction, CancellationToken ct = default)
    {
        await _context.Transactions.AddAsync(transaction, ct);
    }

    public async Task<Account?> GetAccountByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<Account?> GetAccountByTenantIdAsync(string tenantId, CancellationToken ct = default)
    {
        return await _context.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.TenantId == tenantId, ct);
    }

    public async Task AddAccountAsync(Account account, CancellationToken ct = default)
    {
        await _context.Accounts.AddAsync(account, ct);
    }
}
