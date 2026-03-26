namespace FMC.Domain.Common;

/// <summary>
/// Interface to mark entities that require Multi-Tenant isolation.
/// </summary>
public interface ITenantEntity
{
    public string TenantId { get; set; }
}

/// <summary>
/// Base class for domain entities providing identity and multi-tenancy support.
/// </summary>
public abstract class BaseEntity : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
}
