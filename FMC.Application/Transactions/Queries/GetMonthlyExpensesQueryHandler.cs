using FMC.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FMC.Application.Transactions.Queries;

public class GetMonthlyExpensesQueryHandler : IRequestHandler<GetMonthlyExpensesQuery, decimal>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ICurrentUserService _currentUser;

    public GetMonthlyExpensesQueryHandler(IApplicationDbContext context, ICacheService cache, ICurrentUserService currentUser)
    {
        _context = context;
        _cache = cache;
        _currentUser = currentUser;
    }

    public async Task<decimal> Handle(GetMonthlyExpensesQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var tenantId = _currentUser.TenantId ?? "anonymous";
        var cacheKey = $"expenses:{tenantId}:{now.Month}:{now.Year}";

        // Try get from cache
        var cachedResult = await _cache.GetAsync<decimal?>(cacheKey);
        if (cachedResult.HasValue) return cachedResult.Value;

        // Fetch from DB
        var result = await _context.Transactions
            .Where(t => t.Date >= startOfMonth && t.Amount < 0)
            .SumAsync(t => Math.Abs(t.Amount), cancellationToken);

        // Set cache for 10 minutes
        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));

        return result;
    }
}
