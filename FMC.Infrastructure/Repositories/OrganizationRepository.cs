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

    public async Task<IEnumerable<(Organization Org, int UserCount, decimal OrgBalance, decimal UserBalanceSum, string? CeoName)>> GetAllWithStatsAsync(CancellationToken ct = default)
    {
        var orgs = await _context.Organizations.OrderBy(o => o.Name).ToListAsync(ct);
        var orgIds = orgs.Select(o => o.Id).ToList();
        var orgTenantIds = orgIds.Select(id => id.ToString()).ToList();
        var ceoIds = orgs.Where(o => !string.IsNullOrEmpty(o.ChiefExecutiveId)).Select(o => o.ChiefExecutiveId).Distinct().ToList();

        // Batch 1: User Counts
        var userCounts = await _context.Users.OfType<ApplicationUser>()
            .Where(u => u.OrganizationId != null && orgIds.Contains(u.OrganizationId.Value))
            .GroupBy(u => u.OrganizationId)
            .Select(g => new { OrgId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrgId!.Value, x => x.Count, ct);

        // Batch 2: Org Wallet Balances
        var orgBalances = await _context.Accounts
            .IgnoreQueryFilters()
            .Where(a => orgTenantIds.Contains(a.TenantId))
            .Select(a => new { a.TenantId, a.Balance })
            .ToDictionaryAsync(x => x.TenantId, x => x.Balance, ct);

        // Batch 3: Total User Balances (all wallets for that org minus the org's own wallet)
        var totalBalances = await (from u in _context.Users.OfType<ApplicationUser>()
                                   where u.OrganizationId != null && orgIds.Contains(u.OrganizationId.Value)
                                   join a in _context.Accounts.IgnoreQueryFilters() on u.Id equals a.TenantId
                                   group a by u.OrganizationId into g
                                   select new { OrgId = g.Key, Total = g.Sum(x => x.Balance) })
                                   .ToDictionaryAsync(x => x.OrgId!.Value, x => x.Total, ct);

        // Batch 4: CEO Names
        var ceoNames = await _context.Users.OfType<ApplicationUser>()
            .Where(u => ceoIds.Contains(u.Id))
            .Select(u => new { u.Id, FullName = (u.FirstName + " " + u.LastName).Trim() })
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

        var result = new List<(Organization Org, int UserCount, decimal OrgBalance, decimal UserBalanceSum, string? CeoName)>();
        foreach (var org in orgs)
        {
            var count = userCounts.GetValueOrDefault(org.Id, 0);
            var orgBal = orgBalances.GetValueOrDefault(org.Id.ToString(), 0);
            var totalUserBal = totalBalances.GetValueOrDefault(org.Id, 0);
            var ceoName = !string.IsNullOrEmpty(org.ChiefExecutiveId) ? ceoNames.GetValueOrDefault(org.ChiefExecutiveId) : null;

            result.Add((org, count, orgBal, totalUserBal, ceoName));
        }

        return result;
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
