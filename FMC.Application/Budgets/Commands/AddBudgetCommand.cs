using FMC.Shared.DTOs;
using MediatR;

namespace FMC.Application.Budgets.Commands;

public record AddBudgetCommand(BudgetDto Budget) : IRequest<Guid>;
