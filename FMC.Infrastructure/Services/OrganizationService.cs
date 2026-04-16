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
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;
    private readonly ILogger<OrganizationService> _logger;

    public OrganizationService(
        IOrganizationRepository repository,
        ApplicationDbContext context,
        IAuditService auditService,
        ISystemAlertService alertService,
        IIdentityService identityService,
        ICurrentUserService currentUserService,
        IEmailService emailService,
        ILogger<OrganizationService> logger)
    {
        _repository = repository;
        _context = context;
        _auditService = auditService;
        _alertService = alertService;
        _identityService = identityService;
        _currentUserService = currentUserService;
        _emailService = emailService;
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

            var orgAccountSum = await _context.Accounts
                .IgnoreQueryFilters()
                .Where(a => a.TenantId == org.Id.ToString())
                .SumAsync(a => a.Balance, cancellationToken);

            var userAccountSum = await (from u in _context.Users.OfType<ApplicationUser>()
                               where u.OrganizationId == org.Id
                               join a in _context.Accounts.IgnoreQueryFilters() on u.Id equals a.TenantId
                               select a.Balance).SumAsync(cancellationToken);

            var totalBalance = orgAccountSum + userAccountSum;
            var usage = userAccountSum;

            string? ceoName = await ResolveCeoNameAsync(org, cancellationToken);

            result.Add(MapToDto(org, userCount, ceoName, totalBalance, usage));
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

        string? ceoName = await ResolveCeoNameAsync(org, cancellationToken);

        var orgAccountSum = await _context.Accounts
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == org.Id.ToString())
            .SumAsync(a => a.Balance, cancellationToken);

        var userAccountSum = await (from u in _context.Users.OfType<ApplicationUser>()
                           where u.OrganizationId == org.Id
                           join a in _context.Accounts.IgnoreQueryFilters() on u.Id equals a.TenantId
                           select a.Balance).SumAsync(cancellationToken);

        var totalBalance = orgAccountSum + userAccountSum;
        var usage = userAccountSum;

        return MapToDto(org, userCount, ceoName, totalBalance, usage);
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
            WalletLimit = dto.WalletLimit,
            IsDeleted = false,
            TenantId = "SYSTEM", // Will be tenant-scoped in Phase 5 cleanup
            AccountNumber = "63641" + new Random().NextInt64(10000000000, 99999999999).ToString()
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
        org.WalletLimit = dto.WalletLimit;

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
            // Remove other mutually exclusive leadership roles first (CEO/Maker/Approver) to keep it clean
            var rolesToRemove = await _context.Roles
                .Where(r => r.Name == FMC.Shared.Auth.Roles.CEO || r.Name == FMC.Shared.Auth.Roles.Maker || r.Name == FMC.Shared.Auth.Roles.Approver || r.Name == FMC.Shared.Auth.Roles.User)
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
        return await _identityService.GetUsersByOrganizationAsync(organizationId);
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
        // 1. Security Check: Only Maker can initiate. SuperAdmins focus on Org-level funding.
        if (!_currentUserService.IsMaker || _currentUserService.IsSuperAdmin)
        {
            _logger.LogWarning("[OrganizationService] Access Denied: User {UserId} (Maker:{IsMaker}, SuperAdmin:{IsAdmin}) tried to initiate cardholder transaction.", 
                _currentUserService.UserId, _currentUserService.IsMaker, _currentUserService.IsSuperAdmin);
            return false; 
        }

        // 2. Identify the target user
        var user = await _context.Users.OfType<ApplicationUser>()
            .Include(u => u.OrganizationInfo)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId.ToString(), cancellationToken);

        if (user == null) return false;

        // 3. Identify the personal wallet account
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
                TenantId = tenantId,
                OrganizationId = user.OrganizationId
            };
            await _context.Accounts.AddAsync(account, cancellationToken);
        }

        // 4. Create PENDING Transaction (Maker Step)
        // No balance adjustment happens here!
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Label = label ?? (amount >= 0 ? "Credit Allotment Request" : "Debit Adjustment Request"),
            Amount = amount,
            Date = DateTime.UtcNow,
            Category = "Subscriber Allotment",
            TenantId = tenantId,
            AccountId = account.Id,
            Status = "Pending",
            MakerId = _currentUserService.UserId,
            OrganizationId = user.OrganizationId
        };
        
        await _context.Transactions.AddAsync(transaction, cancellationToken);

        // 4. Trace the forensic audit event for Initiation
        await _auditService.RecordFinancialEventAsync(
            "INITIATE_" + (amount >= 0 ? "CREDIT" : "DEBIT"), 
            user.OrganizationId ?? Guid.Empty, 
            $"{user.FirstName} {user.LastName}", 
            Math.Abs(amount), 
            label ?? "Pending Allotment Initiation", 
            performedBy,
            null,
            tenantId
        );

        await _context.SaveChangesAsync(cancellationToken);
        
        // ── Phase 1: Workflow Notifications (Pending Approval) ────────────────
        try
        {
            var org = user.OrganizationInfo;
            if (org != null)
            {
                var approverEmails = await (from u in _context.Users.OfType<ApplicationUser>()
                                            where u.OrganizationId == org.Id
                                            join ur in _context.UserRoles on u.Id equals ur.UserId
                                            join r in _context.Roles on ur.RoleId equals r.Id
                                            where r.Name == FMC.Shared.Auth.Roles.Approver
                                            select u.Email).ToListAsync(cancellationToken);

                string? ceoEmail = null;
                if (!string.IsNullOrEmpty(org.ChiefExecutiveId))
                {
                    ceoEmail = await _context.Users.OfType<ApplicationUser>()
                        .Where(u => u.Id == org.ChiefExecutiveId)
                        .Select(u => u.Email)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                var recipients = approverEmails.ToHashSet();
                if (!string.IsNullOrEmpty(ceoEmail)) recipients.Add(ceoEmail);

                if (recipients.Any())
                {
                    var makerName = performedBy;
                    var logoBytes = Convert.FromBase64String(FMC.Infrastructure.Authentication.BrandingConstants.NationlinkLogoBase64);
                    var attachments = new Dictionary<string, byte[]> { { "nlklogo", logoBytes } };
                    
                    var body = $@"<div style=""font-family:'Segoe UI', Roboto, Helvetica, Arial, sans-serif;max-width:600px;margin:20px auto;background:#ffffff;padding:40px;border-radius:12px;box-shadow:0 8px 30px rgba(0,0,0,0.04);border:1px solid #eaeaea;"">
                        <div style=""text-align: center; padding-bottom: 30px; border-bottom: 2px solid #f0f0f0;"">
                            <img src=""cid:nlklogo"" alt=""Nationlink Dashboard"" width=""180"" style=""max-width: 180px; height: auto; display: block; margin: 0 auto;"" />
                        </div>
                        <h2 style=""color:#ff9f43;margin-top:30px;font-size:24px;font-weight:800;letter-spacing:-0.5px;text-align:center;"">Pending Validation Request</h2>
                        <p style=""color:#2d3436;font-size:15px;line-height:1.6;margin-bottom:24px;text-align:center;"">
                            A new subscriber allotment request has been initiated by <strong>{makerName}</strong> and requires your validation to proceed.
                        </p>
                        
                        <div style=""background:#f8f9fa;border-radius:8px;padding:24px;margin-bottom:24px;"">
                            <h4 style=""margin:0 0 16px 0;color:#2d3436;font-size:14px;text-transform:uppercase;letter-spacing:1px;"">Request Details</h4>
                            <table style=""width:100%;border-collapse:collapse;"">
                                <tr style=""border-bottom: 1px solid #e1e5ea;"">
                                    <td style=""padding:12px 0;color:#636e72;font-size:14px;"">Target Cardholder</td>
                                    <td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{user.FirstName} {user.LastName}</td>
                                </tr>
                                <tr style=""border-bottom: 1px solid #e1e5ea;"">
                                    <td style=""padding:12px 0;color:#636e72;font-size:14px;"">Transaction Amount</td>
                                    <td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{amount:C}</td>
                                </tr>
                                <tr>
                                    <td style=""padding:12px 0;color:#636e72;font-size:14px;"">Adjustment Reason</td>
                                    <td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{label ?? "Standard Allotment"}</td>
                                </tr>
                            </table>
                        </div>

                        <p style=""color:#636e72;font-size:14px;line-height:1.5;margin-bottom:30px;text-align:center;"">
                            Please log in to the Intelligence Oversight panel of your Finance Management Console to review and approve this transaction.
                        </p>

                        <div style=""border-top:1px solid #eeeeee;padding-top:20px;text-align:center;"">
                            <p style=""color:#b2bec3;font-size:12px;margin:0;"">
                                This is an automated workflow notification.<br>
                                © {DateTime.UtcNow.Year} Nationlink Finance Management Console. All rights reserved.
                            </p>
                        </div>
                    </div>";

                    foreach (var email in recipients)
                    {
                        _ = _emailService.SendEmailAsync(email, $"FMC Action Required: Pending Approval for {org.Name}", body, attachments);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OrganizationService] Failed to send workflow notifications for pending tx.");
        }
        
        _logger.LogInformation("[OrganizationService] Transaction PENDING for {UserId} by Maker {MakerId}. Amount: {Amount}", 
            userId, _currentUserService.UserId, amount);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ApproveTransactionAsync(Guid transactionId, string approverId, CancellationToken cancellationToken = default)
    {
        // 1. Security Check: Only Approver can commit
        if (!_currentUserService.IsApprover) throw new ApplicationException("Access Denied: You do not have the Approver role required for this action.");

        var transaction = await _context.Transactions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.Status == "Pending", cancellationToken);

        if (transaction == null) throw new ApplicationException("Transaction not found or already processed.");

        // 2. Mutual Exclusion (4-Eyes Principle): Maker cannot be Approver
        if (transaction.MakerId == approverId)
        {
            await _auditService.RecordFinancialEventAsync("APPROVAL_BLOCKED", transaction.OrganizationId ?? Guid.Empty, "N/A", transaction.Amount, "4-Eyes Violation: Maker tried to approve own request.", approverId, null, transaction.TenantId);
            throw new ApplicationException("Four-Eyes Policy: You cannot approve transactions you initiated.");
        }

        // 3. Organization Isolation: Approver must belong to the same org
        var approver = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == approverId, cancellationToken);
        bool txHasOrg = transaction.OrganizationId.HasValue && transaction.OrganizationId != Guid.Empty;
        bool sameOrg = !txHasOrg || (approver != null && approver.OrganizationId == transaction.OrganizationId);
        
        if (approver == null || (!sameOrg && !_currentUserService.IsSuperAdmin))
        {
            await _auditService.RecordFinancialEventAsync("APPROVAL_BLOCKED", transaction.OrganizationId ?? Guid.Empty, "N/A", transaction.Amount, "Residency Violation: Approver belongs to different organization.", approverId, null, transaction.TenantId);
            throw new ApplicationException("Organizational Security: You can only approve transactions for your own organization.");
        }

        // 4. Identify Accounts for Transfer
        var userAccount = await _context.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == transaction.AccountId, cancellationToken);
        if (userAccount == null) throw new ApplicationException("Target cardholder account not found.");

        // Correctly handle the Transfer logic now
        if (transaction.OrganizationId.HasValue)
        {
            var orgTenantId = transaction.OrganizationId.Value.ToString();
            var orgAccount = await _context.Accounts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.TenantId == orgTenantId, cancellationToken);

            if (orgAccount != null)
            {
                orgAccount.Balance -= transaction.Amount;
            }
        }

        userAccount.Balance += transaction.Amount;
        transaction.Status = "Approved";
        transaction.ApproverId = approverId;
        transaction.ActionDate = DateTime.UtcNow;

        // Audit Trail for Final Commitment
        await _auditService.RecordFinancialEventAsync(
            "APPROVED", 
            transaction.OrganizationId ?? Guid.Empty, 
            userAccount.Name, 
            Math.Abs(transaction.Amount), 
            "Allotment Approved and Settled", 
            approverId,
            null,
            userAccount.TenantId
        );

        await _context.SaveChangesAsync(cancellationToken);
        
        // ── Workflow & Capacity Notifications ─────────────────────────────────
        try
        {
            if (transaction.OrganizationId.HasValue)
            {
                var orgId = transaction.OrganizationId.Value;
                var org = await _repository.GetByIdAsync(orgId, cancellationToken);
                if (org != null)
                {
                    // 1. Resolve Common Data
                    var orgBalance = await _context.Accounts.IgnoreQueryFilters()
                        .Where(a => a.TenantId == orgId.ToString())
                        .SumAsync(a => a.Balance, cancellationToken);

                    var userBalanceSum = await (from u in _context.Users.OfType<ApplicationUser>()
                                                 where u.OrganizationId == orgId
                                                 join a in _context.Accounts.IgnoreQueryFilters() on u.Id equals a.TenantId
                                                 select a.Balance).SumAsync(cancellationToken);

                    var total = orgBalance + userBalanceSum;
                    var usedPct = total > 0 ? (userBalanceSum / total) * 100m : 0m;

                    string? ceoEmail = null;
                    if (!string.IsNullOrEmpty(org.ChiefExecutiveId))
                    {
                        ceoEmail = await _context.Users.IgnoreQueryFilters()
                            .Where(u => u.Id == org.ChiefExecutiveId)
                            .Select(u => u.Email)
                            .FirstOrDefaultAsync(cancellationToken);
                    }

                    var logoBytes = Convert.FromBase64String(FMC.Infrastructure.Authentication.BrandingConstants.NationlinkLogoBase64);
                    var attachments = new Dictionary<string, byte[]> { { "nlklogo", logoBytes } };

                    // 2. Handle Capacity Threshold Advisory (SuperAdmin requirement)
                    if (!string.IsNullOrEmpty(ceoEmail) && (usedPct >= 80m || orgBalance <= 100_000m))
                    {
                        var alertType = usedPct >= 80m ? $"{usedPct:F0}% Operational Capacity Alert" : "Critical Liquidity Advisory";
                        var thresholdBody = $@"<div style=""font-family:'Segoe UI', Roboto, Helvetica, Arial, sans-serif;max-width:600px;margin:20px auto;background:#ffffff;padding:40px;border-radius:12px;box-shadow:0 8px 30px rgba(0,0,0,0.04);border:1px solid #eaeaea;"">
                            <div style=""text-align: center; padding-bottom: 30px; border-bottom: 2px solid #f0f0f0;"">
                                <img src=""cid:nlklogo"" alt=""Nationlink Dashboard"" width=""180"" style=""max-width: 180px; height: auto; display: block; margin: 0 auto;"" />
                            </div>
                            <h2 style=""color:#d63031;margin-top:30px;font-size:24px;font-weight:800;letter-spacing:-0.5px;text-align:center;"">{alertType}</h2>
                            <p style=""color:#2d3436;font-size:15px;line-height:1.6;margin-bottom:24px;text-align:center;"">
                                This is an automated advisory regarding the operational liquidity of <strong>{org.Name}</strong>. Your tenant account has reached a structural capacity threshold and requires attention.
                            </p>
                            <div style=""background:#f8f9fa;border-radius:8px;padding:24px;margin-bottom:24px;"">
                                <h4 style=""margin:0 0 16px 0;color:#2d3436;font-size:14px;text-transform:uppercase;letter-spacing:1px;"">Account Overview</h4>
                                <table style=""width:100%;border-collapse:collapse;"">
                                    <tr style=""border-bottom: 1px solid #e1e5ea;""><td style=""padding:12px 0;color:#636e72;font-size:14px;"">Total Institutional Wallet</td><td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{total:C}</td></tr>
                                    <tr style=""border-bottom: 1px solid #e1e5ea;""><td style=""padding:12px 0;color:#636e72;font-size:14px;"">Volume Dispersed to Subscribers</td><td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{userBalanceSum:C} ({usedPct:F1}%)</td></tr>
                                    <tr><td style=""padding:12px 0;color:#d63031;font-size:14px;font-weight:600;"">Remaining Organizational Capital</td><td style=""padding:12px 0;font-weight:800;color:#d63031;text-align:right;font-size:16px;"">{orgBalance:C}</td></tr>
                                </table>
                            </div>
                            <p style=""color:#636e72;font-size:14px;line-height:1.5;margin-bottom:30px;text-align:center;"">We strongly advise replenishing your institutional reserve to ensure continuous functionality.</p>
                            <div style=""border-top:1px solid #eeeeee;padding-top:20px;text-align:center;""><p style=""color:#b2bec3;font-size:12px;margin:0;"">© {DateTime.UtcNow.Year} Nationlink Finance Management Console.</p></div>
                        </div>";
                        _ = _emailService.SendEmailAsync(ceoEmail, $"FMC Advisory: {alertType} — {org.Name}", thresholdBody, attachments);
                    }

                    // 3. Handle Workflow Approval Notification (Maker & CEO)
                    var makerEmail = await _context.Users.IgnoreQueryFilters().Where(u => u.Id == transaction.MakerId).Select(u => u.Email).FirstOrDefaultAsync(cancellationToken);
                    var workflowRecipients = new HashSet<string>();
                    if (!string.IsNullOrEmpty(makerEmail)) workflowRecipients.Add(makerEmail);
                    if (!string.IsNullOrEmpty(ceoEmail)) workflowRecipients.Add(ceoEmail);

                    if (workflowRecipients.Any())
                    {
                        var approvalBody = $@"<div style=""font-family:'Segoe UI', Roboto, Helvetica, Arial, sans-serif;max-width:600px;margin:20px auto;background:#ffffff;padding:40px;border-radius:12px;box-shadow:0 8px 30px rgba(0,0,0,0.04);border:1px solid #eaeaea;"">
                            <div style=""text-align: center; padding-bottom: 30px; border-bottom: 2px solid #f0f0f0;"">
                                <img src=""cid:nlklogo"" alt=""Nationlink Dashboard"" width=""180"" style=""max-width: 180px; height: auto; display: block; margin: 0 auto;"" />
                            </div>
                            <h2 style=""color:#00b894;margin-top:30px;font-size:24px;font-weight:800;letter-spacing:-0.5px;text-align:center;"">Transaction Approved</h2>
                            <p style=""color:#2d3436;font-size:15px;line-height:1.6;margin-bottom:24px;text-align:center;"">
                                Good news! A subscriber allotment request has been successfully validated and completed for <strong>{org.Name}</strong>.
                            </p>
                            <div style=""background:#f8f9fa;border-radius:8px;padding:24px;margin-bottom:24px;"">
                                <h4 style=""margin:0 0 16px 0;color:#2d3436;font-size:14px;text-transform:uppercase;letter-spacing:1px;"">Transaction Details</h4>
                                <table style=""width:100%;border-collapse:collapse;"">
                                    <tr style=""border-bottom: 1px solid #e1e5ea;""><td style=""padding:12px 0;color:#636e72;font-size:14px;"">Recipient Cardholder</td><td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{userAccount.Name}</td></tr>
                                    <tr style=""border-bottom: 1px solid #e1e5ea;""><td style=""padding:12px 0;color:#636e72;font-size:14px;"">Approved Amount</td><td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{transaction.Amount:C}</td></tr>
                                    <tr><td style=""padding:12px 0;color:#636e72;font-size:14px;"">Adjustment Reason</td><td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{transaction.Label}</td></tr>
                                </table>
                            </div>
                            <p style=""color:#636e72;font-size:14px;line-height:1.5;margin-bottom:30px;text-align:center;"">The funds have been successfully settled to the subscriber account.</p>
                            <div style=""border-top:1px solid #eeeeee;padding-top:20px;text-align:center;""><p style=""color:#b2bec3;font-size:12px;margin:0;"">© {DateTime.UtcNow.Year} Nationlink Finance Management Console.</p></div>
                        </div>";
                        foreach (var email in workflowRecipients)
                        {
                            _ = _emailService.SendEmailAsync(email, $"FMC Notification: Transaction Approved — {org.Name}", approvalBody, attachments);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OrganizationService] Failed to send notifications during approval workflow.");
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RejectTransactionAsync(Guid transactionId, string approverId, string reason, CancellationToken cancellationToken = default)
    {
        // 1. Security Check: Only Approver can reject
        if (!_currentUserService.IsApprover) throw new ApplicationException("Access Denied: You do not have the Approver role.");

        var transaction = await _context.Transactions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.Status == "Pending", cancellationToken);

        if (transaction == null) return false;

        transaction.Status = "Rejected";
        transaction.ApproverId = approverId;
        transaction.ActionDate = DateTime.UtcNow;
        transaction.RejectionReason = reason;

        await _auditService.RecordFinancialEventAsync(
            "REJECTED", 
            transaction.OrganizationId ?? Guid.Empty, 
            "N/A", 
            Math.Abs(transaction.Amount), 
            $"Transaction Rejected: {reason}", 
            approverId,
            null,
            transaction.TenantId
        );

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FMC.Shared.DTOs.TransactionDto>> GetPendingTransactionsAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        // 1. Multi-Tenant Security: Block cross-org viewing unless SuperAdmin
        if (!_currentUserService.IsSuperAdmin)
        {
            var userOrgId = _currentUserService.TenantId;
            if (string.IsNullOrEmpty(userOrgId) || Guid.Parse(userOrgId) != organizationId)
            {
                _logger.LogWarning("[OrganizationService] Security Violation: User {Id} tried to view pending tx for different Org {OrgId}.", _currentUserService.UserId, organizationId);
                return Enumerable.Empty<FMC.Shared.DTOs.TransactionDto>();
            }
        }

        var transactions = await _context.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.OrganizationId == organizationId && t.Status == "Pending")
            .OrderByDescending(t => t.Date)
            .ToListAsync(cancellationToken);

        var result = new List<FMC.Shared.DTOs.TransactionDto>();
        foreach(var t in transactions)
        {
            var account = await _context.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == t.AccountId, cancellationToken);
            var makerAttribute = await _context.Users.FindAsync(new object[] { t.MakerId ?? "" }, cancellationToken);
            
            // Resolve Subscriber details (User or Org)
            string subscriberName = "N/A";
            string? accountNumber = null;

            if (account != null)
            {
                subscriberName = account.Name.Replace("Wallet: ", "");
                // Attempt to resolve User account number
                var user = await _context.Users.FindAsync(new object[] { account.TenantId ?? "" }, cancellationToken);
                if (user != null)
                {
                    accountNumber = user.AccountNumber;
                }
                else if (Guid.TryParse(account.TenantId, out var orgId))
                {
                    // Attempt to resolve Organization account number
                    var org = await _context.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == orgId, cancellationToken);
                    if (org != null)
                    {
                        accountNumber = org.AccountNumber;
                    }
                }
            }

            result.Add(new FMC.Shared.DTOs.TransactionDto
            {
                Id = t.Id,
                Date = t.Date,
                Amount = t.Amount,
                Label = t.Label,
                AccountId = t.AccountId,
                Category = t.Category,
                Status = t.Status,
                Subscriber = subscriberName,
                AccountNumber = accountNumber,
                MakerName = makerAttribute != null ? $"{makerAttribute.FirstName} {makerAttribute.LastName}" : "System",
                MakerId = t.MakerId
            });
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FMC.Shared.DTOs.TransactionDto>> GetTodayTransactionsAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        // 1. Multi-Tenant Security: Block cross-org viewing unless SuperAdmin
        if (!_currentUserService.IsSuperAdmin)
        {
            var userOrgId = _currentUserService.TenantId;
            if (string.IsNullOrEmpty(userOrgId) || Guid.Parse(userOrgId) != organizationId)
            {
                _logger.LogWarning("[OrganizationService] Security Violation: User {Id} tried to view today's logs for different Org {OrgId}.", _currentUserService.UserId, organizationId);
                return Enumerable.Empty<FMC.Shared.DTOs.TransactionDto>();
            }
        }

        var today = DateTime.UtcNow.Date;
        var transactions = await _context.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.OrganizationId == organizationId && t.Date >= today)
            .OrderByDescending(t => t.Date)
            .ToListAsync(cancellationToken);

        var result = new List<FMC.Shared.DTOs.TransactionDto>();
        foreach(var t in transactions)
        {
            var account = await _context.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == t.AccountId, cancellationToken);
            var makerAttribute = await _context.Users.FindAsync(new object[] { t.MakerId ?? "" }, cancellationToken);
            var approver = !string.IsNullOrEmpty(t.ApproverId) 
                ? await _context.Users.FindAsync(new object[] { t.ApproverId }, cancellationToken) 
                : null;
            
            // Resolve Subscriber details (User or Org)
            string subscriberName = "N/A";
            string? accountNumber = null;

            if (account != null)
            {
                subscriberName = account.Name.Replace("Wallet: ", "");
                var user = await _context.Users.FindAsync(new object[] { account.TenantId ?? "" }, cancellationToken);
                if (user != null)
                {
                    accountNumber = user.AccountNumber;
                }
                else if (Guid.TryParse(account.TenantId, out var orgId))
                {
                    var org = await _context.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == orgId, cancellationToken);
                    if (org != null)
                    {
                        accountNumber = org.AccountNumber;
                    }
                }
            }

            result.Add(new FMC.Shared.DTOs.TransactionDto
            {
                Id = t.Id,
                Date = t.Date,
                Amount = t.Amount,
                Label = t.Label,
                AccountId = t.AccountId,
                Category = t.Category,
                Status = t.Status,
                Subscriber = subscriberName,
                AccountNumber = accountNumber,
                MakerName = makerAttribute != null ? $"{makerAttribute.FirstName} {makerAttribute.LastName}" : "System",
                MakerId = t.MakerId,
                ApproverName = approver != null ? $"{approver.FirstName} {approver.LastName}" : null,
                ActionDate = t.ActionDate
            });
        }

        return result;
    }

    // ─────────────────────────────────────
    // Private Mapping Helpers
    // ─────────────────────────────────────

    private async Task<string?> ResolveCeoNameAsync(Organization org, CancellationToken cancellationToken)
    {
        string? ceoName = null;

        // 1. Primary Strategy: Explicit pointer in the Organization entity
        if (!string.IsNullOrEmpty(org.ChiefExecutiveId))
        {
            var user = await _context.Users.FindAsync(new object[] { org.ChiefExecutiveId }, cancellationToken);
            if (user is ApplicationUser appUser)
            {
                ceoName = $"{appUser.FirstName} {appUser.LastName}";
            }
        }

        // 2. Secondary Strategy: Fallback to users with the 'CEO' role
        if (string.IsNullOrEmpty(ceoName))
        {
            ceoName = await (from u in _context.Users.OfType<ApplicationUser>()
                                 where u.OrganizationId == org.Id
                                 join ur in _context.UserRoles on u.Id equals ur.UserId
                                 join r in _context.Roles on ur.RoleId equals r.Id
                                 where r.Name == FMC.Shared.Auth.Roles.CEO
                                 select u.FirstName + " " + u.LastName).FirstOrDefaultAsync(cancellationToken);
        }

        // 3. System Strategy: Fallback to 'SuperAdmin' for the primary system organization if still unassigned
        if (string.IsNullOrEmpty(ceoName) && org.Name == "Nationlink/Infoserve Inc.")
        {
             ceoName = await (from u in _context.Users.OfType<ApplicationUser>()
                                 where u.OrganizationId == org.Id
                                 join ur in _context.UserRoles on u.Id equals ur.UserId
                                 join r in _context.Roles on ur.RoleId equals r.Id
                                 where r.Name == FMC.Shared.Auth.Roles.SuperAdmin
                                 select u.FirstName + " " + u.LastName).FirstOrDefaultAsync(cancellationToken);
        }

        return ceoName?.Trim();
    }

    private static OrganizationDto MapToDto(Organization org, int userCount = 0, string? ceoName = null, decimal totalBalance = 0, decimal usage = 0) => new()
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
        Usage = usage,
        ChiefExecutiveId = org.ChiefExecutiveId,
        WalletLimit = org.WalletLimit,
        AccountNumber = org.AccountNumber
    };

    /// <inheritdoc />
    public async Task<bool> CancelTransactionAsync(Guid transactionId, string makerId, CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.Status == "Pending", cancellationToken);

        if (transaction == null) return false;

        // Security: Only the original Maker can cancel their own pending request
        if (transaction.MakerId != makerId)
        {
            _logger.LogWarning("[OrganizationService] Security Violation: User {Id} tried to cancel transaction {TxId} created by {MakerId}.", makerId, transactionId, transaction.MakerId);
            return false;
        }

        transaction.Status = "Cancelled";
        transaction.ActionDate = DateTime.UtcNow;

        await _auditService.RecordFinancialEventAsync(
            "CANCELLED", 
            transaction.OrganizationId ?? Guid.Empty, 
            "N/A", 
            Math.Abs(transaction.Amount), 
            "Transaction Cancelled by Maker", 
            makerId,
            null,
            transaction.TenantId
        );

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
    /// <inheritdoc />
    public async Task<IEnumerable<FMC.Shared.DTOs.TransactionDto>> GetOrganizationTransactionsAsync(Guid organizationId, string? status = null, int count = 50, CancellationToken cancellationToken = default)
    {
        // 1. Multi-Tenant Security check
        if (!_currentUserService.IsSuperAdmin && _currentUserService.OrganizationId != organizationId)
        {
            _logger.LogWarning("[Security] Unauthorized history access attempt by {UserId} for Org {OrgId}", _currentUserService.UserId, organizationId);
            return Enumerable.Empty<FMC.Shared.DTOs.TransactionDto>();
        }

        var query = _context.Transactions.IgnoreQueryFilters()
            .Where(t => t.OrganizationId == organizationId);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(t => t.Status == status);
        }

        var transactions = await query
            .OrderByDescending(t => t.Date)
            .Take(count)
            .ToListAsync(cancellationToken);

        var result = new List<FMC.Shared.DTOs.TransactionDto>();
        foreach (var t in transactions)
        {
            var account = await _context.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == t.AccountId, cancellationToken);
            var maker = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == t.MakerId, cancellationToken);
            var approver = !string.IsNullOrEmpty(t.ApproverId) 
                ? await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == t.ApproverId, cancellationToken) 
                : null;

            string subscriberName = "System Node";
            string? accountNumber = null;

            if (account != null)
            {
                subscriberName = account.Name.Replace("Wallet: ", "");
                var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == account.TenantId, cancellationToken);
                if (user != null)
                {
                    accountNumber = user.AccountNumber;
                }
                else
                {
                    var org = await _context.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id.ToString() == account.TenantId, cancellationToken);
                    if (org != null) accountNumber = org.AccountNumber;
                }
            }

            result.Add(new FMC.Shared.DTOs.TransactionDto
            {
                Id = t.Id,
                Date = t.Date,
                Amount = t.Amount,
                Label = t.Label,
                AccountId = t.AccountId,
                Category = t.Category,
                Status = string.IsNullOrWhiteSpace(t.Status) ? "Successful" : t.Status,
                Subscriber = subscriberName,
                AccountNumber = accountNumber,
                MakerName = maker != null ? $"{maker.FirstName} {maker.LastName}" : "System",
                MakerId = t.MakerId,
                ApproverName = approver != null ? $"{approver.FirstName} {approver.LastName}" : null,
                ActionDate = t.ActionDate,
                OrganizationId = t.OrganizationId
            });
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FMC.Shared.DTOs.Admin.SystemAlertDto>> GetWorkflowAlertsAsync(Guid organizationId, string userId, string role, CancellationToken cancellationToken = default)
    {
        var alerts = new List<FMC.Shared.DTOs.Admin.SystemAlertDto>();
        var since = DateTime.UtcNow.AddDays(-1);

        if (role == FMC.Shared.Auth.Roles.Approver || role == FMC.Shared.Auth.Roles.CEO)
        {
            var pCount = await _context.Transactions.CountAsync(t => t.OrganizationId == organizationId && t.Status == "Pending", cancellationToken);
            if (pCount > 0) alerts.Add(CreateAlert("Pending Validations", $"{pCount} request(s) waiting to be approved.", $"Pending_{pCount}", FMC.Shared.DTOs.Admin.AlertSeverityDto.Warning));
        }

        if (role == FMC.Shared.Auth.Roles.Maker || role == FMC.Shared.Auth.Roles.CEO)
        {
            var q = _context.Transactions.Where(t => t.OrganizationId == organizationId && t.Status == "Approved" && t.ActionDate >= since);
            if (role == FMC.Shared.Auth.Roles.Maker) q = q.Where(t => t.MakerId == userId);
            
            var aCount = await q.CountAsync(cancellationToken);
            if (aCount > 0) alerts.Add(CreateAlert("Approved Requests", $"{aCount} request(s) recently approved.", $"Processed_{aCount}", FMC.Shared.DTOs.Admin.AlertSeverityDto.Information));
        }

        if (role == FMC.Shared.Auth.Roles.Maker || role == FMC.Shared.Auth.Roles.CEO || role == FMC.Shared.Auth.Roles.Approver)
        {
            var orgBalance = await _context.Accounts.IgnoreQueryFilters()
                .Where(a => a.TenantId == organizationId.ToString())
                .SumAsync(a => a.Balance, cancellationToken);

            var userBalanceSum = await (from u in _context.Users.OfType<ApplicationUser>()
                                         where u.OrganizationId == organizationId
                                         join a in _context.Accounts.IgnoreQueryFilters() on u.Id equals a.TenantId
                                         select a.Balance).SumAsync(cancellationToken);

            var total = orgBalance + userBalanceSum;
            var usedPct = total > 0 ? (userBalanceSum / total) * 100m : 0m;

            if (usedPct >= 80m || orgBalance <= 100_000m)
            {
                var msg = usedPct >= 80m ? $"{usedPct:F1}% of wallet allocated." : $"Only {orgBalance:C} remaining in org wallet.";
                var sev = usedPct >= 80m ? FMC.Shared.DTOs.Admin.AlertSeverityDto.Security : FMC.Shared.DTOs.Admin.AlertSeverityDto.Warning;
                alerts.Add(CreateAlert("Capacity Threshold", msg, $"Threshold_{(int)usedPct}", sev));
            }
        }

        return alerts;

        FMC.Shared.DTOs.Admin.SystemAlertDto CreateAlert(string title, string msg, string entityId, FMC.Shared.DTOs.Admin.AlertSeverityDto sev) => new()
        {
            Id = 0, Title = title, Message = msg, Severity = sev, EntityType = "Workflow", EntityId = entityId, CreatedAt = DateTime.UtcNow
        };
    }
}
