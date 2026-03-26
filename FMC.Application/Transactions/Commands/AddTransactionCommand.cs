using FMC.Shared.DTOs;
using MediatR;

namespace FMC.Application.Transactions.Commands;

public record AddTransactionCommand(TransactionDto Transaction) : IRequest<Guid>;
