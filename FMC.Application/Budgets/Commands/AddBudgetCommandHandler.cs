using FMC.Application.Interfaces;
using FMC.Domain.Entities;
using MediatR;

namespace FMC.Application.Budgets.Commands;

public class AddBudgetCommandHandler : IRequestHandler<AddBudgetCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public AddBudgetCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(AddBudgetCommand request, CancellationToken cancellationToken)
    {
        var entity = new Budget
        {
            Id = Guid.NewGuid(),
            Category = request.Budget.Category,
            Limit = request.Budget.Limit,
            Period = request.Budget.Period
        };

        _context.Budgets.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
