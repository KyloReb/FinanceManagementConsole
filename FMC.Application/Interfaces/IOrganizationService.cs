using FMC.Shared.DTOs.Organization;

namespace FMC.Application.Interfaces;

/// <summary>
/// Database-agnostic service abstraction for all Organization business operations.
/// All implementations are injected via DI — swap the concrete class freely without touching call sites.
/// </summary>
public interface IOrganizationService
{
    /// <summary>
    /// Retrieves all active (non-soft-deleted) organizations visible within the current tenant context.
    /// Results are ordered alphabetically by name for consistent UI rendering.
    /// </summary>
    Task<IEnumerable<OrganizationDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single organization by its unique UUID.
    /// Returns null if the record does not exist or has been soft-deleted.
    /// </summary>
    /// <param name="id">The UUID of the target organization.</param>
    Task<OrganizationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new organization into the system.
    /// Enforces uniqueness by rejecting duplicate names within the same tenant scope.
    /// </summary>
    /// <param name="dto">The creation payload containing name and optional description.</param>
    /// <returns>The fully hydrated DTO of the newly created organization.</returns>
    /// <exception cref="InvalidOperationException">Thrown if an organization with the same name already exists.</exception>
    Task<OrganizationDto> CreateAsync(CreateOrganizationDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies field-level mutations to an existing organization record.
    /// Automatically stamps the UpdatedAt audit footprint.
    /// </summary>
    /// <param name="dto">The update payload containing the target Id and revised field values.</param>
    /// <returns>True if the update was committed successfully; false if the record was not found.</returns>
    Task<bool> UpdateAsync(UpdateOrganizationDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes an existing organization record.
    /// Returns false if no matching record is found.
    /// </summary>
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all users affiliated with a specific organization.
    /// </summary>
    Task<IEnumerable<FMC.Shared.DTOs.User.UserDto>> GetUsersByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a balance adjustment (Debit/Credit) for a specific organization and logs the action.
    /// </summary>
    Task<bool> AdjustBalanceAsync(Guid organizationId, decimal amount, string label, string performedBy, CancellationToken cancellationToken = default);
}
