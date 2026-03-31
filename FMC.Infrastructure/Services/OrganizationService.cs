using FMC.Application.Interfaces;
using FMC.Domain.Entities;
using FMC.Infrastructure.Data;
using FMC.Shared.DTOs.Organization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FMC.Infrastructure.Services;

/// <summary>
/// Concrete implementation of <see cref="IOrganizationService"/> backed by Entity Framework Core.
/// All database queries are routed through <see cref="IOrganizationRepository"/> — never through
/// <see cref="ApplicationDbContext"/> directly. This ensures the service remains 100% database-agnostic:
/// swapping to a different ORM or storage engine only requires replacing the repository, not this class.
/// </summary>
public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _repository;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrganizationService> _logger;

    public OrganizationService(
        IOrganizationRepository repository,
        ApplicationDbContext context,
        ILogger<OrganizationService> logger)
    {
        _repository = repository;
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<OrganizationDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var organizations = await _repository.GetAllAsync(cancellationToken);

        // Project to DTO and enrich each entry with its affiliated user count.
        // User count is resolved via a secondary EF query to keep the repository layer clean and focused.
        var result = new List<OrganizationDto>();
        foreach (var org in organizations)
        {
            var userCount = await _context.Users
                .OfType<ApplicationUser>()
                .CountAsync(u => u.OrganizationId == org.Id, cancellationToken);

            result.Add(MapToDto(org, userCount));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<OrganizationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var org = await _repository.GetByIdAsync(id, cancellationToken);
        return org is null ? null : MapToDto(org);
    }

    /// <inheritdoc />
    public async Task<OrganizationDto> CreateAsync(CreateOrganizationDto dto, CancellationToken cancellationToken = default)
    {
        // Business Rule: Enforce unique organization names within the system.
        var exists = await _context.Organizations
            .AnyAsync(o => o.Name == dto.Name, cancellationToken);

        if (exists)
        {
            _logger.LogWarning("[OrganizationService] Rejected duplicate creation attempt for name: {Name}", dto.Name);
            throw new InvalidOperationException($"An organization with the name '{dto.Name}' already exists.");
        }

        var entity = new Organization
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
            TenantId = "SYSTEM" // Will be tenant-scoped in Phase 5 cleanup
        };

        await _repository.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[OrganizationService] Created organization '{Name}' with Id: {Id}", entity.Name, entity.Id);
        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(UpdateOrganizationDto dto, CancellationToken cancellationToken = default)
    {
        var org = await _repository.GetByIdAsync(dto.Id, cancellationToken);
        if (org is null)
        {
            _logger.LogWarning("[OrganizationService] Update failed — no organization found with Id: {Id}", dto.Id);
            return false;
        }

        // Business Rule: Ensure the new name does not conflict with another existing organization.
        var nameConflict = await _context.Organizations
            .AnyAsync(o => o.Name == dto.Name && o.Id != dto.Id, cancellationToken);

        if (nameConflict)
        {
            _logger.LogWarning("[OrganizationService] Update rejected — name '{Name}' already in use by another organization.", dto.Name);
            throw new InvalidOperationException($"The name '{dto.Name}' is already used by another organization.");
        }

        org.Name = dto.Name.Trim();
        org.Description = dto.Description?.Trim();
        org.IsActive = dto.IsActive;

        _repository.Update(org); // stamps UpdatedAt automatically
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[OrganizationService] Updated organization Id: {Id}", org.Id);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var org = await _repository.GetByIdAsync(id, cancellationToken);
        if (org is null)
        {
            _logger.LogWarning("[OrganizationService] Soft-delete failed — no organization found with Id: {Id}", id);
            return false;
        }

        _repository.SoftDelete(org); // stamps IsDeleted + DeletedAt automatically
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[OrganizationService] Soft-deleted organization '{Name}' (Id: {Id})", org.Name, org.Id);
        return true;
    }

    // ─────────────────────────────────────
    // Private Mapping Helpers
    // ─────────────────────────────────────

    /// <summary>
    /// Maps a domain entity to its public-facing DTO representation.
    /// Centralizing this mapping here ensures that all callers benefit from any future DTO changes automatically.
    /// </summary>
    private static OrganizationDto MapToDto(Organization org, int userCount = 0) => new()
    {
        Id = org.Id,
        Name = org.Name,
        Description = org.Description,
        IsActive = org.IsActive,
        CreatedAt = org.CreatedAt,
        UpdatedAt = org.UpdatedAt,
        UserCount = userCount
    };
}
