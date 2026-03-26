using FMC.Shared.DTOs;
using MediatR;

namespace FMC.Application.Budgets.Queries;

public record GetBudgetsQuery : IRequest<List<BudgetDto>>;
