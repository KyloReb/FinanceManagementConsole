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

        // Batch 1: User Counts (Actual Web Project Users/AspNetUsers)
        var userCounts = await _context.Users.OfType<ApplicationUser>()
            .Where(u => u.OrganizationId.HasValue && orgIds.Contains(u.OrganizationId.Value))
            .GroupBy(u => u.OrganizationId!.Value)
            .Select(g => new { OrgId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrgId, x => x.Count, ct);

        // Batch 2: Org Wallet Balances
        var orgBalances = await _context.Accounts
            .IgnoreQueryFilters()
            .Where(a => orgTenantIds.Contains(a.TenantId))
            .Select(a => new { a.TenantId, a.Balance })
            .ToDictionaryAsync(x => x.TenantId, x => x.Balance, ct);

        // Batch 3: Total User Balances (all wallets for that org minus the org's own wallet)
        var totalBalances = await (from c in _context.Cardholders
                                   where orgIds.Contains(c.OrganizationId)
                                   join a in _context.Accounts.IgnoreQueryFilters() on c.Id.ToString() equals a.TenantId
                                   group a by c.OrganizationId into g
                                   select new { OrgId = g.Key, Total = g.Sum(x => x.Balance) })
                                   .ToDictionaryAsync(x => x.OrgId, x => x.Total, ct);

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
        return await (from c in _context.Cardholders
                      where c.OrganizationId == organizationId
                      join a in _context.Accounts.IgnoreQueryFilters() on c.Id.ToString() equals a.TenantId
                      where a.TenantId != orgTenantId
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

    public async Task<Cardholder?> GetCardholderByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Cardholders.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<Cardholder?> GetCardholderByAccountNumberAsync(string accountNumber, CancellationToken ct = default)
    {
        return await _context.Cardholders.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.AccountNumber == accountNumber, ct);
    }

    public async Task<Cardholder?> GetCardholderByAccountNumberAsync(string accountNumber, Guid organizationId, CancellationToken ct = default)
    {
        return await _context.Cardholders.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.AccountNumber == accountNumber && c.OrganizationId == organizationId, ct);
    }

    public async Task<IEnumerable<Cardholder>> GetAllCardholdersAsync(CancellationToken ct = default)
    {
        return await _context.Cardholders.Include(c => c.Organization).ToListAsync(ct);
    }

    public async Task<IEnumerable<Cardholder>> GetCardholdersByOrganizationAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await _context.Cardholders
            .Include(c => c.Organization)
            .Where(c => c.OrganizationId == organizationId)
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToListAsync(ct);
    }

    public async Task<Transaction?> GetTransactionByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Transactions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByStatusAsync(Guid organizationId, string status, CancellationToken ct = default)
    {
        var query = _context.Transactions.IgnoreQueryFilters().Where(t => t.Status == status);
        
        if (organizationId != Guid.Empty)
        {
            query = query.Where(t => t.OrganizationId == organizationId);
        }

        return await query.OrderByDescending(t => t.Date).ToListAsync(ct);
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

    public async Task<IEnumerable<Transaction>> GetTransactionsByBatchIdAsync(Guid batchId, CancellationToken ct = default)
    {
        return await _context.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.BatchId == batchId)
            .ToListAsync(ct);
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

    public async Task<Account?> GetAccountByCardNumberAsync(string cardNumber, Guid organizationId, CancellationToken ct = default)
    {
        var cardholder = await _context.Cardholders.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.AccountNumber == cardNumber && c.OrganizationId == organizationId, ct);

        if (cardholder == null) return null;

        // Try primary account link (Cardholder ID)
        var account = await GetAccountByTenantIdAsync(cardholder.Id.ToString(), ct);
        
        // Fallback: Try legacy account link (IdentityUserId) if it's a migrated user
        if (account == null && !string.IsNullOrEmpty(cardholder.IdentityUserId))
        {
            account = await GetAccountByTenantIdAsync(cardholder.IdentityUserId, ct);
        }

        return account;
    }

    public async Task AddAccountAsync(Account account, CancellationToken ct = default)
    {
        await _context.Accounts.AddAsync(account, ct);
    }

    public async Task<bool> ExistsTransactionWithIdempotencyKeyAsync(string key, CancellationToken ct = default)
    {
        return await _context.Transactions.AnyAsync(t => t.IdempotencyKey == key, ct);
    }
}
