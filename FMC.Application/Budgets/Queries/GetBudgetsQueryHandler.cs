using FMC.Application.Interfaces;
using FMC.Shared.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FMC.Application.Budgets.Queries;

public class GetBudgetsQueryHandler : IRequestHandler<GetBudgetsQuery, List<BudgetDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBudgetsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<BudgetDto>> Handle(GetBudgetsQuery request, CancellationToken cancellationToken)
    {
        return await _context.Budgets
            .AsNoTracking()
            .Select(b => new BudgetDto
            {
                Id = b.Id,
                Category = b.Category,
                Limit = b.Limit,
                Period = b.Period
            })
            .ToListAsync(cancellationToken);
    }
}
