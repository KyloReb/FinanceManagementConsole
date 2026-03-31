using Microsoft.EntityFrameworkCore;
using FMC.Application.Interfaces;
using FMC.Domain.Entities;
using FMC.Infrastructure.Data;

namespace FMC.Infrastructure.Repositories;

/// <summary>
/// Contains the specific Entity Framework dialect configurations answering the Interface contracts reliably dynamically scaling context filters.
/// </summary>
public class OrganizationRepository : IOrganizationRepository
{
    private readonly ApplicationDbContext _context;

    public OrganizationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Organization>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        await _context.Organizations.AddAsync(organization, cancellationToken);
    }

    public void Update(Organization organization)
    {
        organization.UpdatedAt = DateTime.UtcNow;
        _context.Organizations.Update(organization);
    }

    public void SoftDelete(Organization organization)
    {
        // Safe logical removal keeping history intact but hiding from the native contextual lookups
        organization.IsDeleted = true;
        organization.DeletedAt = DateTime.UtcNow;
        _context.Organizations.Update(organization);
    }
}
