using FMC.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FMC.Application.Accounts.Queries;

public class GetTotalBalanceQueryHandler : IRequestHandler<GetTotalBalanceQuery, decimal>
{
    private readonly IApplicationDbContext _context;

    public GetTotalBalanceQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> Handle(GetTotalBalanceQuery request, CancellationToken cancellationToken)
    {
        return await _context.Accounts.SumAsync(a => a.Balance, cancellationToken);
    }
}
