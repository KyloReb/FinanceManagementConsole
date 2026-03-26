using FMC.Shared.DTOs;
using MediatR;

namespace FMC.Application.Transactions.Queries;

public record GetRecentTransactionsQuery(int Count) : IRequest<List<TransactionDto>>;
