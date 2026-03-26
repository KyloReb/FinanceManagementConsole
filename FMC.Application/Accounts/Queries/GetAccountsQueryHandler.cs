using FMC.Application.Interfaces;
using FMC.Shared.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FMC.Application.Accounts.Queries;

public class GetAccountsQueryHandler : IRequestHandler<GetAccountsQuery, List<AccountDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAccountsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AccountDto>> Handle(GetAccountsQuery request, CancellationToken cancellationToken)
    {
        return await _context.Accounts
            .AsNoTracking()
            .Select(a => new AccountDto
            {
                Id = a.Id,
                Name = a.Name,
                Balance = a.Balance
            })
            .ToListAsync(cancellationToken);
    }
}
