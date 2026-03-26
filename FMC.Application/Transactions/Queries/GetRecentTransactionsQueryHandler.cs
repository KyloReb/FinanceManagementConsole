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
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                Date = t.Date,
                Amount = t.Amount,
                Label = t.Label,
                AccountId = t.AccountId,
                Category = t.Category
            })
            .ToListAsync(cancellationToken);
    }
}
