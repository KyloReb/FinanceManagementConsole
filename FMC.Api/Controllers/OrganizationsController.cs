using FMC.Application.Interfaces;
using FMC.Shared.Auth;
using FMC.Shared.DTOs.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FMC.Api.Controllers;

/// <summary>
/// RESTful API surface for managing Organizations.
/// All endpoints are restricted to SuperAdmin role to prevent unauthorized tenant data mutation.
/// Route: /api/organizations
/// </summary>
[Authorize(Roles = Roles.SuperAdmin + "," + Roles.CEO)]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OrganizationsController : ControllerBase
{
    private readonly FMC.Application.Interfaces.IOrganizationService _organizationService;
    private readonly ILogger<OrganizationsController> _logger;

    public OrganizationsController(
        FMC.Application.Interfaces.IOrganizationService organizationService,
        ILogger<OrganizationsController> logger)
    {
        _organizationService = organizationService;
        _logger = logger;
    }

    /// <summary>
    /// Performs a balance adjustment (Debit/Credit) for a specific organization.
    /// This will automatically target the Core Operations Wallet of the tenant.
    /// </summary>
    /// <param name="id">The unique identifier of the organization/tenant.</param>
    /// <param name="request">The adjustment payload (Amount & Label).</param>
    /// <response code="200">Balance successfully updated.</response>
    /// <response code="404">No organization or account found corresponding to the ID.</response>
    [HttpPost("{id:guid}/adjust-balance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdjustBalance(Guid id, [FromBody] BalanceAdjustmentRequest request)
    {
        _logger.LogInformation("[OrganizationsController] Requesting balance adjustment for Tenant {Id} of {Amount} by {User}", id, request.Amount, User.Identity?.Name);
        
        var performedBy = User.Identity?.Name ?? "System";
        var success = await _organizationService.AdjustBalanceAsync(id, request.Amount, request.Label, performedBy, HttpContext.RequestAborted);
        return success ? Ok() : NotFound();
    }

    public record BalanceAdjustmentRequest(decimal Amount, string Label);

    /// <summary>
    /// Retrieves all active organizations, ordered alphabetically.
    /// </summary>
    /// <response code="200">Returns the list of active organizations.</response>
    /// <response code="200">Returns the list of active organizations.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OrganizationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OrganizationDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _organizationService.GetAllAsync(cancellationToken);

        if (User.IsInRole(Roles.CEO))
        {
            var userOrgClaim = User.FindFirst("OrganizationId")?.Value;
            if (string.IsNullOrEmpty(userOrgClaim) || !Guid.TryParse(userOrgClaim, out var claimId))
            {
                return Ok(Enumerable.Empty<OrganizationDto>());
            }

            return Ok(result.Where(o => o.Id == claimId).ToList());
        }

        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single organization by its UUID.
    /// </summary>
    /// <param name="id">The unique identifier of the organization.</param>
    /// <response code="200">Returns the matching organization.</response>
    /// <response code="404">No active organization was found with the given ID.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _organizationService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Registers a new organization into the system.
    /// Returns a 409 Conflict if the organization name already exists.
    /// </summary>
    /// <param name="dto">The creation payload.</param>
    /// <response code="201">Organization successfully created.</response>
    /// <response code="400">Validation error in the request payload.</response>
    /// <response code="409">An organization with the same name already exists.</response>
    [HttpPost]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrganizationDto>> Create(
        [FromBody] CreateOrganizationDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var created = await _organizationService.CreateAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("[OrganizationsController] Conflict on Create: {Message}", ex.Message);
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Applies mutations to an existing organization record.
    /// Returns a 409 Conflict if the new name is already claimed by another organization.
    /// </summary>
    /// <param name="dto">The update payload including the target Id.</param>
    /// <response code="204">Organization successfully updated.</response>
    /// <response code="400">Validation error in the request payload.</response>
    /// <response code="404">No active organization was found with the given ID.</response>
    /// <response code="409">The revised name conflicts with another existing organization.</response>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateOrganizationDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var updated = await _organizationService.UpdateAsync(dto, cancellationToken);
            return updated ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("[OrganizationsController] Conflict on Update: {Message}", ex.Message);
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Soft-deletes an organization, hiding it from all active queries.
    /// The historical record and all user affiliations are preserved in the database.
    /// </summary>
    /// <param name="id">The UUID of the organization to logically remove.</param>
    /// <response code="204">Organization successfully soft-deleted.</response>
    /// <response code="404">No active organization was found with the given ID.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _organizationService.SoftDeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// Retrieves all users affiliated with the specified organization.
    /// </summary>
    /// <param name="id">The unique identifier of the organization.</param>
    /// <response code="200">Returns the list of affiliated users.</response>
    [HttpGet("{id:guid}/users")]
    [ProducesResponseType(typeof(IEnumerable<FMC.Shared.DTOs.User.UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FMC.Shared.DTOs.User.UserDto>>> GetUsers(Guid id, CancellationToken cancellationToken)
    {
        var users = await _organizationService.GetUsersByOrganizationAsync(id, cancellationToken);
        return Ok(users);
    }
    /// <summary>
    /// CEO Endpoint: Retrieves high-fidelity metrics for the organization.
    /// </summary>
    [HttpGet("{id:guid}/dashboard-metrics")]
    public async Task<ActionResult<OrganizationDashboardMetricsDto>> GetDashboardMetrics(Guid id)
    {
        // Security: CEOs can only see their own organization, SuperAdmins see all.
        if (User.IsInRole(Roles.CEO))
        {
            var userOrgClaim = User.FindFirst("OrganizationId")?.Value;
            if (string.IsNullOrEmpty(userOrgClaim)) return Forbid();
            if (!Guid.TryParse(userOrgClaim, out var claimId) || claimId != id)
                return Forbid();
        }

        var org = await _organizationService.GetByIdAsync(id, HttpContext.RequestAborted);
        if (org == null) return NotFound();

        // Calculate metrics
        var metrics = new OrganizationDashboardMetricsDto
        {
            TotalBalance = org.TotalBalance,
            UserCount = org.UserCount,
            DailyVolume = 0, // Future: aggregate from transactions
            WalletLimit = org.WalletLimit
        };

        return Ok(metrics);
    }
}
