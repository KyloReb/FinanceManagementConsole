using FMC.Application.Accounts.Queries;
using FMC.Shared.DTOs;
using FMC.Shared.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FMC.Api.Controllers;

[Authorize(Roles = $"{Roles.SuperAdmin},{Roles.User},{Roles.Maker},{Roles.Approver},{Roles.CEO}")]
[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<List<AccountDto>>> GetAccounts()
    {
        return await _mediator.Send(new GetAccountsQuery());
    }

    [HttpGet("balance/total")]
    public async Task<ActionResult<decimal>> GetTotalBalance()
    {
        return await _mediator.Send(new GetTotalBalanceQuery());
    }
}
