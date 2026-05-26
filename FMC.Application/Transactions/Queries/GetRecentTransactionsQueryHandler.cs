using FMC.Application.Interfaces;
using FMC.Shared.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using FMC.Domain.Entities;

namespace FMC.Application.Transactions.Queries;

public class GetRecentTransactionsQueryHandler : IRequestHandler<GetRecentTransactionsQuery, List<TransactionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;

    public GetRecentTransactionsQueryHandler(IApplicationDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<TransactionDto>> Handle(GetRecentTransactionsQuery request, CancellationToken cancellationToken)
    {
        string cacheKey = $"RecentTransactions_{request.Count}";
        var cached = await _cache.GetAsync<List<TransactionDto>>(cacheKey);
        if (cached != null) return cached;

        var transactions = await _context.Transactions
            .AsNoTracking()
            .OrderByDescending(t => t.ActionDate ?? t.Date)
            .Take(request.Count)
            .ToListAsync(cancellationToken);

        // Pre-load all reference data into dictionaries to eliminate N+1 queries
        var allTenantIds = transactions.Select(t => t.TenantId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var allMakerIds = transactions.Select(t => t.MakerId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var allApproverIds = transactions.Select(t => t.ApproverId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var allUserIds = allTenantIds.Concat(allMakerIds).Concat(allApproverIds).Distinct().ToList();

        var orgs = await _context.Organizations.IgnoreQueryFilters()
            .Where(o => allTenantIds.Contains(o.Id.ToString()))
            .ToListAsync(cancellationToken);
        var orgMap = orgs.ToDictionary(o => o.Id.ToString(), o => o, StringComparer.OrdinalIgnoreCase);

        var users = await _context.Users.IgnoreQueryFilters()
            .Where(u => allUserIds.Contains(u.Id))
            .ToListAsync(cancellationToken);
        var userMap = users.ToDictionary(u => u.Id, u => u, StringComparer.OrdinalIgnoreCase);

        var cardholders = await _context.Cardholders.IgnoreQueryFilters()
            .Where(c => allTenantIds.Contains(c.Id.ToString()))
            .ToListAsync(cancellationToken);
        var cardholderMap = cardholders.ToDictionary(c => c.Id.ToString(), c => c, StringComparer.OrdinalIgnoreCase);

        var result = new List<TransactionDto>(transactions.Count);
        foreach (var t in transactions)
        {
            string subscriber = "System Node";
            string? accountNumber = null;
            string? makerName = "System";
            string? approverName = null;

            if (!string.IsNullOrEmpty(t.TenantId))
            {
                if (orgMap.TryGetValue(t.TenantId, out var org))
                {
                    subscriber = org.Name;
                    accountNumber = org.AccountNumber;
                }
                else if (userMap.TryGetValue(t.TenantId, out var user))
                {
                    subscriber = $"{user.FirstName} {user.LastName}";
                }
                else if (cardholderMap.TryGetValue(t.TenantId, out var cardholder))
                {
                    subscriber = $"{cardholder.FirstName} {cardholder.LastName}";
                    accountNumber = cardholder.AccountNumber;
                }
            }

            if (!string.IsNullOrEmpty(t.MakerId) && userMap.TryGetValue(t.MakerId, out var maker))
            {
                makerName = $"{maker.FirstName} {maker.LastName}";
            }

            if (!string.IsNullOrEmpty(t.ApproverId) && userMap.TryGetValue(t.ApproverId, out var approver))
            {
                approverName = $"{approver.FirstName} {approver.LastName}";
            }

            result.Add(new TransactionDto
            {
                Id = t.Id,
                Date = t.Date,
                Amount = t.Amount,
                Label = t.Label,
                AccountId = t.AccountId,
                Category = t.Category,
                Subscriber = subscriber,
                AccountNumber = accountNumber,
                Status = string.IsNullOrWhiteSpace(t.Status) ? "Successful" : t.Status,
                MakerName = makerName,
                MakerId = t.MakerId,
                OrganizationId = t.OrganizationId,
                ActionDate = t.ActionDate,
                RejectionReason = t.RejectionReason,
                ApproverName = approverName
            });
        }

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2));

        return result;
    }
}
