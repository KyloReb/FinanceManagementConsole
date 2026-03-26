using MediatR;

namespace FMC.Application.Accounts.Queries;

public record GetTotalBalanceQuery : IRequest<decimal>;
