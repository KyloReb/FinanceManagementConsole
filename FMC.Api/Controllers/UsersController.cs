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

    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.CEO + "," + Roles.Maker + "," + Roles.Approver)]
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll()
    {
        if (!User.IsInRole(Roles.SuperAdmin))
        {
            var orgIdStr = User.FindFirst("OrganizationId")?.Value;
            if (string.IsNullOrEmpty(orgIdStr) || !Guid.TryParse(orgIdStr, out var orgId)) 
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

    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.CEO + "," + Roles.Maker + "," + Roles.Approver)]
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(string id)
    {
        var user = await _identityService.GetUserByIdAsync(id);
        if (user == null) return NotFound();

        if (!User.IsInRole(Roles.SuperAdmin))
        {
            var myOrgId = User.FindFirst("OrganizationId")?.Value;
            if (string.IsNullOrEmpty(myOrgId) || user.OrganizationId?.ToString() != myOrgId)
            {
                return Forbid();
            }
        }

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

    [Authorize(Roles = Roles.Maker)]
    [HttpPost("{id:guid}/adjust-balance")]
    public async Task<IActionResult> AdjustBalance(Guid id, [FromBody] UserBalanceAdjustmentRequest request)
    {
        var performedBy = User.Identity?.Name ?? "System";

        // Security check for Org isolation (Maker must belong to same org as target)
        var targetUser = await _identityService.GetUserByIdAsync(id.ToString());
        if (targetUser == null) return NotFound("Target user not found.");

        var makerOrgId = User.FindFirst("OrganizationId")?.Value;
        if (string.IsNullOrEmpty(makerOrgId) || targetUser.OrganizationId?.ToString() != makerOrgId)
        {
            return Forbid();
        }

        var success = await _organizationService.AdjustUserBalanceAsync(id, request.Amount, request.Label, performedBy, HttpContext.RequestAborted);
        return success ? Ok() : BadRequest("Failed to initiate adjustment request. Ensure you have the Maker role.");
    }

    [Authorize(Roles = Roles.Approver)]
    [HttpPost("transactions/{transactionId:guid}/approve")]
    public async Task<IActionResult> ApproveTransaction(Guid transactionId)
    {
        var approverId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(approverId)) return Unauthorized();

        try 
        {
            var success = await _organizationService.ApproveTransactionAsync(transactionId, approverId, HttpContext.RequestAborted);
            return success ? Ok() : BadRequest("Approval failed. Transaction may be in an invalid state.");
        }
        catch (ApplicationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = Roles.Approver)]
    [HttpPost("transactions/{transactionId:guid}/reject")]
    public async Task<IActionResult> RejectTransaction(Guid transactionId, [FromBody] RejectRequest request)
    {
        var approverId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(approverId)) return Unauthorized();

        var success = await _organizationService.RejectTransactionAsync(transactionId, approverId, request.Reason, HttpContext.RequestAborted);
        return success ? Ok() : BadRequest("Rejection failed.");
    }

    [Authorize(Roles = Roles.Maker)]
    [HttpDelete("transactions/{transactionId:guid}/cancel")]
    public async Task<IActionResult> CancelTransaction(Guid transactionId)
    {
        var makerId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(makerId)) return Unauthorized();

        var success = await _organizationService.CancelTransactionAsync(transactionId, makerId, HttpContext.RequestAborted);
        return success ? Ok() : BadRequest("Cancellation failed. You can only cancel your own pending transactions.");
    }

    [Authorize(Roles = Roles.Approver + "," + Roles.CEO + "," + Roles.SuperAdmin + "," + Roles.Maker)]
    [HttpGet("organizations/{orgId:guid}/pending-transactions")]
    public async Task<ActionResult<List<TransactionDto>>> GetPendingTransactions(Guid orgId)
    {
        // Security check for CEO (can only see their own org's pending)
        if (User.IsInRole(Roles.CEO) || User.IsInRole(Roles.Approver))
        {
            var myOrgId = User.FindFirst("OrganizationId")?.Value;
            if (string.IsNullOrEmpty(myOrgId) || Guid.Parse(myOrgId) != orgId) return Forbid();
        }

        var transactions = await _organizationService.GetPendingTransactionsAsync(orgId);
        return Ok(transactions);
    }

    [Authorize(Roles = Roles.Approver + "," + Roles.CEO + "," + Roles.SuperAdmin + "," + Roles.Maker)]
    [HttpGet("organizations/{orgId:guid}/transactions")]
    public async Task<ActionResult<List<TransactionDto>>> GetOrganizationTransactions(Guid orgId, [FromQuery] string? status = null, [FromQuery] int count = 50)
    {
        if (!User.IsInRole(Roles.SuperAdmin))
        {
            var myOrgId = User.FindFirst("OrganizationId")?.Value;
            if (string.IsNullOrEmpty(myOrgId) || Guid.Parse(myOrgId) != orgId) return Forbid();
        }

        var transactions = await _organizationService.GetOrganizationTransactionsAsync(orgId, status, count);
        return Ok(transactions.ToList());
    }

    [Authorize(Roles = Roles.Approver + "," + Roles.CEO + "," + Roles.SuperAdmin + "," + Roles.Maker)]
    [HttpGet("organizations/{orgId:guid}/today-transactions")]
    public async Task<ActionResult<List<TransactionDto>>> GetTodayTransactions(Guid orgId)
    {
        if (User.IsInRole(Roles.CEO) || User.IsInRole(Roles.Approver) || User.IsInRole(Roles.Maker))
        {
            var myOrgId = User.FindFirst("OrganizationId")?.Value;
            if (string.IsNullOrEmpty(myOrgId) || Guid.Parse(myOrgId) != orgId) return Forbid();
        }

        var transactions = await _organizationService.GetTodayTransactionsAsync(orgId);
        return Ok(transactions);
    }

    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.CEO + "," + Roles.Maker + "," + Roles.Approver)]
    [HttpGet("{id}/transactions")]
    public async Task<ActionResult<List<TransactionDto>>> GetTransactions(string id, [FromQuery] int count = 10)
    {
        var targetUser = await _identityService.GetUserByIdAsync(id);
        if (targetUser == null) return NotFound();

        // Security check for non-SuperAdmins (CEO, Maker, Approver)
        if (!User.IsInRole(Roles.SuperAdmin))
        {
            var myOrgId = User.FindFirst("OrganizationId")?.Value;
            if (string.IsNullOrEmpty(myOrgId) || targetUser.OrganizationId?.ToString() != myOrgId)
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
    public record RejectRequest(string Reason);
}
