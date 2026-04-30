using FMC.Application.Interfaces;
using FMC.Shared.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using FMC.Domain.Entities;

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
        var transactions = await _context.Transactions
            .AsNoTracking()
            .OrderByDescending(t => t.Date)
            .Take(request.Count)
            .ToListAsync(cancellationToken);

        var result = new List<TransactionDto>();
        foreach (var t in transactions)
        {
            string subscriber = "System Node";
            string? accountNumber = null;
            string? makerName = "System";

            // Resolve Subscriber by TenantId (can be UserId or OrgId)
            var org = await _context.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id.ToString() == t.TenantId, cancellationToken);
            if (org != null)
            {
                subscriber = org.Name;
                accountNumber = org.AccountNumber;
            }
            else
            {
                var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == t.TenantId, cancellationToken);
                if (user != null)
                {
                    subscriber = $"{user.FirstName} {user.LastName}";
                }
            }

            // Resolve Maker
            if (!string.IsNullOrEmpty(t.MakerId))
            {
                var maker = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == t.MakerId, cancellationToken);
                if (maker != null) makerName = $"{maker.FirstName} {maker.LastName}";
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
                OrganizationId = t.OrganizationId
            });
        }

        return result;
    }
}
