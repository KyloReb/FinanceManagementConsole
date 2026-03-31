using FMC.Domain.Entities;

namespace FMC.Application.Interfaces;

/// <summary>
/// Data access abstraction for managing Organizations globally in a database-agnostic format.
/// Supports Enterprise scaling decoupling the core logic from specific ORM technologies.
/// </summary>
public interface IOrganizationRepository
{
    /// <summary>
    /// Locates an organization uniquely by its UUID sequence safely supporting soft deletion avoidance dynamically.
    /// </summary>
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires all organizations registered safely evaluating active tenancy scaling logic transparently.
    /// </summary>
    Task<IEnumerable<Organization>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches a newly minted organization entity into the core storage sequence awaiting synchronous transactions.
    /// </summary>
    Task AddAsync(Organization organization, CancellationToken cancellationToken = default);

    /// <summary>
    /// Overwrites entity attributes safely preventing data overlap securely propagating timestamp updates natively.
    /// </summary>
    void Update(Organization organization);

    /// <summary>
    /// Logically tags the organizational structure as obsolete without physically destroying its footprint natively.
    /// </summary>
    void SoftDelete(Organization organization);
}
