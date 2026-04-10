using FMC.Application.Interfaces;
using FMC.Shared.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FMC.Application.Transactions.Queries;

public class GetRecentTransactionsQueryHandler : IRequestHandler<GetRecentTransactionsQuery, List<TransactionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetRecentTransactionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<TransactionDto>> Handle(GetRecentTransactionsQuery request, CancellationToken cancellationToken)
    {
        return await _context.Transactions
            .AsNoTracking()
            .OrderByDescending(t => t.Date)
            .Take(request.Count)
            .Select(t => new 
            { 
                T = t, 
                OrgName = _context.Organizations
                    .Where(o => o.Id.ToString() == t.TenantId)
                    .Select(o => o.Name)
                    .FirstOrDefault() 
            })
            .Select(x => new TransactionDto
            {
                Id = x.T.Id,
                Date = x.T.Date,
                Amount = x.T.Amount,
                Label = x.T.Label,
                AccountId = x.T.AccountId,
                Category = x.T.Category,
                Subscriber = x.OrgName ?? "System Node",
                Status = string.IsNullOrWhiteSpace(x.T.Status) ? "Successful" : x.T.Status
            })
            .ToListAsync(cancellationToken);
    }
}
