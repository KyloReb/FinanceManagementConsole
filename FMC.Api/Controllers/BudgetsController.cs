using FMC.Application.Budgets.Commands;
using FMC.Application.Budgets.Queries;
using FMC.Shared.DTOs;
using FMC.Shared.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FMC.Api.Controllers;

[Authorize(Policy = Roles.Manager)]
[ApiController]
[Route("api/[controller]")]
public class BudgetsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BudgetsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<List<BudgetDto>>> GetBudgets()
    {
        return await _mediator.Send(new GetBudgetsQuery());
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> AddBudget([FromBody] BudgetDto budget)
    {
        var id = await _mediator.Send(new AddBudgetCommand(budget));
        return Ok(id);
    }
}
