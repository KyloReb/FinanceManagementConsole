using FMC.Shared.DTOs;
using MediatR;

namespace FMC.Application.Accounts.Queries;

public record GetAccountsQuery : IRequest<List<AccountDto>>;
