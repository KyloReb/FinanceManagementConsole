using MediatR;

namespace FMC.Application.Transactions.Queries;

public record GetMonthlyExpensesQuery : IRequest<decimal>;
