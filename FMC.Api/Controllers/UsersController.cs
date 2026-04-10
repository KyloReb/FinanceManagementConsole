using FMC.Application.Interfaces;
using FMC.Shared.Auth;
using FMC.Shared.DTOs.User;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FMC.Application.Transactions.Queries;
using FMC.Shared.DTOs;

namespace FMC.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly IOrganizationService _organizationService;
    private readonly IMediator _mediator;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IIdentityService identityService, 
        IOrganizationService organizationService,
        IMediator mediator,
        ILogger<UsersController> logger)
    {
        _identityService = identityService;
        _organizationService = organizationService;
        _mediator = mediator;
        _logger = logger;
    }

    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.CEO)]
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll()
    {
        if (User.IsInRole(Roles.CEO))
        {
            var ceoOrgId = User.FindFirst("OrganizationId")?.Value;
            if (string.IsNullOrEmpty(ceoOrgId) || !Guid.TryParse(ceoOrgId, out var orgId)) 
            {
                return Ok(new List<UserDto>());
            }
            
            return Ok(await _identityService.GetUsersByOrganizationAsync(orgId));
        }

        return Ok(await _identityService.GetAllUsersAsync());
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = await _identityService.GetUserByIdAsync(userId);
        if (user == null) return NotFound();
        return Ok(user);
    }

    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.CEO)]
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(string id)
    {
        var user = await _identityService.GetUserByIdAsync(id);
        if (user == null) return NotFound();
        return Ok(user);
    }

    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.CEO)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto request)
    {
        var result = await _identityService.CreateUserAsync(request);
        if (!result) return BadRequest("Failed to create user.");
        return Ok();
    }

    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.CEO)]
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateUserDto request)
    {
        var result = await _identityService.UpdateUserAsync(request);
        if (!result) return BadRequest("Failed to update user.");
        return Ok();
    }

    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.CEO)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _identityService.DeleteUserAsync(id);
        if (!result) return BadRequest("Failed to delete user.");
        return Ok();
    }

    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.CEO)]
    [HttpPost("{id:guid}/adjust-balance")]
    public async Task<IActionResult> AdjustBalance(Guid id, [FromBody] UserBalanceAdjustmentRequest request)
    {
        var performedBy = User.Identity?.Name ?? "System";

        // Security check for CEO role
        if (User.IsInRole(Roles.CEO))
        {
            var targetUser = await _identityService.GetUserByIdAsync(id.ToString());
            if (targetUser == null) return NotFound("Target user not found.");

            var ceoOrgId = User.FindFirst("OrganizationId")?.Value;
            if (string.IsNullOrEmpty(ceoOrgId) || targetUser.OrganizationId?.ToString() != ceoOrgId)
            {
                return Forbid();
            }
        }

        var success = await _organizationService.AdjustUserBalanceAsync(id, request.Amount, request.Label, performedBy, HttpContext.RequestAborted);
        return success ? Ok() : BadRequest("Failed to adjust user balance.");
    }

    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.CEO)]
    [HttpGet("{id}/transactions")]
    public async Task<ActionResult<List<TransactionDto>>> GetTransactions(string id, [FromQuery] int count = 10)
    {
        var targetUser = await _identityService.GetUserByIdAsync(id);
        if (targetUser == null) return NotFound();

        // 1. Security check for CEO role
        if (User.IsInRole(Roles.CEO))
        {
            var ceoOrgId = User.FindFirst("OrganizationId")?.Value;
            if (string.IsNullOrEmpty(ceoOrgId) || targetUser.OrganizationId?.ToString() != ceoOrgId)
            {
                return Forbid();
            }
        }

        // 2. Resolve the effective TenantId for the target user (OrgId if present, else UserId)
        var effectiveTenantId = targetUser.OrganizationId?.ToString() ?? targetUser.Id;

        // 3. Request transactions for strictly resolved tenant context
        return await _mediator.Send(new GetUserTransactionsQuery(effectiveTenantId, count));
    }

    public record UserBalanceAdjustmentRequest(decimal Amount, string Label);
}
