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
    private readonly IAuditService _auditService;
    private readonly ISystemAlertService _alertService;
    private readonly ILogger<OrganizationService> _logger;

    public OrganizationService(
        IOrganizationRepository repository,
        ApplicationDbContext context,
        IAuditService auditService,
        ISystemAlertService alertService,
        ILogger<OrganizationService> logger)
    {
        _repository = repository;
        _context = context;
        _auditService = auditService;
        _alertService = alertService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<OrganizationDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var organizations = await _repository.GetAllAsync(cancellationToken);

        var result = new List<OrganizationDto>();
        foreach (var org in organizations)
        {
            var userCount = await _context.Users
                .OfType<ApplicationUser>()
                .CountAsync(u => u.OrganizationId == org.Id, cancellationToken);

            var totalBalance = await _context.Accounts
                .IgnoreQueryFilters()
                .Where(a => a.TenantId == org.Id.ToString())
                .SumAsync(a => a.Balance, cancellationToken);

            string? ceoName = null;
            if (!string.IsNullOrEmpty(org.ChiefExecutiveId))
            {
                var ceo = await _context.Users.FindAsync(new object[] { org.ChiefExecutiveId }, cancellationToken);
                if (ceo is ApplicationUser appUser)
                {
                    ceoName = $"{appUser.FirstName} {appUser.LastName}";
                }
            }

            if (string.IsNullOrEmpty(ceoName))
            {
                // Fallback to role-based lookup if no explicit ChiefExecutiveId is set
                ceoName = await (from u in _context.Users.OfType<ApplicationUser>()
                                     where u.OrganizationId == org.Id
                                     join ur in _context.UserRoles on u.Id equals ur.UserId
                                     join r in _context.Roles on ur.RoleId equals r.Id
                                     where r.Name == FMC.Shared.Auth.Roles.CEO
                                     select u.FirstName + " " + u.LastName).FirstOrDefaultAsync(cancellationToken);
            }

            result.Add(MapToDto(org, userCount, ceoName?.Trim(), totalBalance));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<OrganizationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var org = await _repository.GetByIdAsync(id, cancellationToken);
        if (org == null) return null;

        var userCount = await _context.Users
            .OfType<ApplicationUser>()
            .CountAsync(u => u.OrganizationId == org.Id, cancellationToken);

        string? ceoName = null;
        if (!string.IsNullOrEmpty(org.ChiefExecutiveId))
        {
            var ceo = await _context.Users.FindAsync(new object[] { org.ChiefExecutiveId }, cancellationToken);
            if (ceo is ApplicationUser appUser)
            {
                ceoName = $"{appUser.FirstName} {appUser.LastName}";
            }
        }

        var totalBalance = await _context.Accounts
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == org.Id.ToString())
            .SumAsync(a => a.Balance, cancellationToken);

        return MapToDto(org, userCount, ceoName, totalBalance);
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

        // Synchronize Roles if the Chief Executive was changed
        if (org.ChiefExecutiveId != dto.ChiefExecutiveId)
        {
            var oldCeoId = org.ChiefExecutiveId;
            var newCeoId = dto.ChiefExecutiveId;
            org.ChiefExecutiveId = newCeoId;

            // 1. Assign CEO role to the new leader
            if (!string.IsNullOrEmpty(newCeoId))
            {
                await EnsureUserHasRoleAsync(newCeoId, FMC.Shared.Auth.Roles.CEO, cancellationToken);
            }

            // 2. (Optional) You might want to demote the old CEO to 'User' or 'Manager', 
            // but for now we'll prioritize making sure the new one is correctly set.
        }

        _repository.Update(org); // stamps UpdatedAt automatically
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[OrganizationService] Updated organization Id: {Id} and synchronized leadership roles.", org.Id);
        return true;
    }

    /// <summary>
    /// Ensures a user has a specific role assigned in the Identity system.
    /// Uses raw SQL/Context approach within the infrastructure layer to avoid circular dependencies with UserManager.
    /// </summary>
    private async Task EnsureUserHasRoleAsync(string userId, string roleName, CancellationToken cancellationToken)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role == null) return;

        var hasRole = await _context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == role.Id, cancellationToken);
        if (!hasRole)
        {
            // Remove other mutually exclusive leadership roles first (CEO/Manager) to keep it clean
            var rolesToRemove = await _context.Roles
                .Where(r => r.Name == FMC.Shared.Auth.Roles.CEO || r.Name == FMC.Shared.Auth.Roles.Manager || r.Name == FMC.Shared.Auth.Roles.User)
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            var existingRoles = _context.UserRoles.Where(ur => ur.UserId == userId && rolesToRemove.Contains(ur.RoleId));
            _context.UserRoles.RemoveRange(existingRoles);

            // Add the new role
            await _context.UserRoles.AddAsync(new IdentityUserRole<string> { UserId = userId, RoleId = role.Id }, cancellationToken);
            _logger.LogInformation("[OrganizationService] Role '{Role}' synchronized for user {UserId}", roleName, userId);
        }
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

    /// <inheritdoc />
    public async Task<IEnumerable<FMC.Shared.DTOs.User.UserDto>> GetUsersByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var users = await _context.Users
            .OfType<ApplicationUser>()
            .Where(u => u.OrganizationId == organizationId)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken);

        var result = new List<FMC.Shared.DTOs.User.UserDto>();

        foreach (var user in users)
        {
            var roles = await (from ur in _context.UserRoles
                               where ur.UserId == user.Id
                               join r in _context.Roles on ur.RoleId equals r.Id
                               select r.Name).ToListAsync(cancellationToken);

            result.Add(new FMC.Shared.DTOs.User.UserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                Roles = roles // Ensure roles are populated
            });
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> AdjustBalanceAsync(Guid organizationId, decimal amount, string label, string performedBy, CancellationToken cancellationToken = default)
    {
        // 1. Ensure the organization exists
        var org = await _repository.GetByIdAsync(organizationId, cancellationToken);
        if (org == null) return false;

        // 2. Find or create the primary operations account for this tenant
        var tenantId = organizationId.ToString();
        var account = await _context.Accounts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId, cancellationToken);

        if (account == null)
        {
            account = new Account
            {
                Id = Guid.NewGuid(),
                Name = "Core Operations Wallet",
                Balance = 0,
                TenantId = tenantId
            };
            await _context.Accounts.AddAsync(account, cancellationToken);
        }

        // 3. Create the transaction record
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Label = label ?? (amount >= 0 ? "System Credit" : "System Debit"),
            Amount = amount,
            Date = DateTime.UtcNow,
            Category = "System Adjustment",
            TenantId = tenantId
        };

        // 4. Update the actual account balance
        account.Balance += amount;

        // 5. Automated Intelligence: Raise alert for negative equity
        if (account.Balance < 0)
        {
            await _alertService.RaiseAlertAsync(
                "Negative Ledger Balance", 
                $"Tenant '{org.Name}' is operating with negative liquidity ({account.Balance:C}). Immediate settlement or suspension recommended.", 
                AlertSeverity.Warning, 
                organizationId.ToString(), 
                "Organization"
            );
        }

        // 6. Log the Audit Event
        var actionType = amount >= 0 ? "CREDIT" : "DEBIT";
        await _auditService.RecordFinancialEventAsync(
            actionType, 
            organizationId, 
            org.Name, 
            Math.Abs(amount), 
            label ?? "Ledger Adjustment", 
            performedBy,
            null,
            organizationId.ToString()
        );

        await _context.Transactions.AddAsync(transaction, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[OrganizationService] Successfully adjusted balance for Tenant {Id} by {Amount}. New Balance: {NewBalance}", 
            tenantId, amount, account.Balance);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> AdjustUserBalanceAsync(Guid userId, decimal amount, string label, string performedBy, CancellationToken cancellationToken = default)
    {
        // 1. Identify the user and their organization context (for logging)
        var user = await _context.Users.OfType<ApplicationUser>()
            .Include(u => u.OrganizationInfo)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId.ToString(), cancellationToken);

        if (user == null) return false;

        // 2. Identify the personal wallet account (TenantId = UserId)
        var tenantId = user.Id;
        var account = await _context.Accounts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId, cancellationToken);

        if (account == null)
        {
            account = new Account
            {
                Id = Guid.NewGuid(),
                Name = $"Wallet: {user.FirstName} {user.LastName}",
                Balance = 0,
                TenantId = tenantId
            };
            await _context.Accounts.AddAsync(account, cancellationToken);
        }

        // 3. Persist individual transaction
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Label = label ?? (amount >= 0 ? "Adjustment Credit" : "Adjustment Debit"),
            Amount = amount,
            Date = DateTime.UtcNow,
            Category = "Subscriber Allotment",
            TenantId = tenantId,
            AccountId = account.Id
        };
        
        account.Balance += amount;
        await _context.Transactions.AddAsync(transaction, cancellationToken);

        // 4. Trace the forensic audit event
        var actionType = amount >= 0 ? "CREDIT" : "DEBIT";
        await _auditService.RecordFinancialEventAsync(
            actionType, 
            user.OrganizationId ?? Guid.Empty, 
            $"{user.FirstName} {user.LastName}", 
            Math.Abs(amount), 
            label ?? "Individual Performance Adjustment", 
            performedBy,
            null,
            tenantId
        );

        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("[OrganizationService] Adjusted USER balance for {UserId} by {Amount}. Label: {Label}", 
            userId, amount, label);

        return true;
    }

    // ─────────────────────────────────────
    // Private Mapping Helpers
    // ─────────────────────────────────────

    private static OrganizationDto MapToDto(Organization org, int userCount = 0, string? ceoName = null, decimal totalBalance = 0) => new()
    {
        Id = org.Id,
        Name = org.Name,
        Description = org.Description,
        IsActive = org.IsActive,
        CreatedAt = org.CreatedAt,
        UpdatedAt = org.UpdatedAt,
        UserCount = userCount,
        CeoName = ceoName,
        TotalBalance = totalBalance,
        ChiefExecutiveId = org.ChiefExecutiveId
    };
}
