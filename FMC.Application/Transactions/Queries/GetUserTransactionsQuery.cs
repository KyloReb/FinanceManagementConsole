using FMC.Application.Interfaces;
using FMC.Shared.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FMC.Application.Transactions.Queries;

public record GetUserTransactionsQuery(string TenantId, int Count) : IRequest<List<TransactionDto>>;

public class GetUserTransactionsQueryHandler : IRequestHandler<GetUserTransactionsQuery, List<TransactionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetUserTransactionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<TransactionDto>> Handle(GetUserTransactionsQuery request, CancellationToken cancellationToken)
    {
        // 1. Query transactions bound strictly to this tenant's economic sphere
        return await _context.Transactions
            .IgnoreQueryFilters() 
            .Where(t => t.TenantId == request.TenantId)
            .OrderByDescending(t => t.Date)
            .Take(request.Count)
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                Date = t.Date,
                Amount = t.Amount,
                Label = t.Label,
                AccountId = t.AccountId,
                Category = t.Category,
                Status = t.Status,
                MakerId = t.MakerId,
                OrganizationId = t.OrganizationId,
                BatchId = t.BatchId
            })
            .ToListAsync(cancellationToken);
    }
}
