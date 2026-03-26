using FMC.Application.Transactions.Commands;
using FMC.Application.Transactions.Queries;
using FMC.Shared.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FMC.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TransactionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("recent")]
    public async Task<ActionResult<List<TransactionDto>>> GetRecent([FromQuery] int count = 10)
    {
        return await _mediator.Send(new GetRecentTransactionsQuery(count));
    }

    [HttpGet("expenses/monthly")]
    public async Task<ActionResult<decimal>> GetMonthlyExpenses()
    {
        return await _mediator.Send(new GetMonthlyExpensesQuery());
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> AddTransaction([FromBody] TransactionDto transaction)
    {
        var id = await _mediator.Send(new AddTransactionCommand(transaction));
        return Ok(id);
    }
}
