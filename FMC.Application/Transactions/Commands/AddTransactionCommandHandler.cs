using FMC.Application.Interfaces;
using FMC.Domain.Entities;
using MediatR;

namespace FMC.Application.Transactions.Commands;

public class AddTransactionCommandHandler : IRequestHandler<AddTransactionCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public AddTransactionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(AddTransactionCommand request, CancellationToken cancellationToken)
    {
        var entity = new Transaction
        {
            Id = Guid.NewGuid(),
            Date = request.Transaction.Date,
            Amount = request.Transaction.Amount,
            Label = request.Transaction.Label,
            AccountId = request.Transaction.AccountId,
            Category = request.Transaction.Category
        };

        _context.Transactions.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
