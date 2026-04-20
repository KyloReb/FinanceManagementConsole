using FMC.Application.Interfaces;
using FMC.Application.Organizations.Events;
using FMC.Domain.Entities;
using FMC.Infrastructure.Data;
using FMC.Shared.DTOs.Organization;
using MediatR;
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
    private readonly IAuditService _auditService;
    private readonly IPublisher _publisher;
    private readonly ILedgerService _ledgerService;
    private readonly ISystemAlertService _alertService;
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<OrganizationService> _logger;

    public OrganizationService(
        IOrganizationRepository repository,
        IAuditService auditService,
        IPublisher publisher,
        ILedgerService ledgerService,
        IIdentityService identityService,
        ICurrentUserService currentUserService,
        ISystemAlertService alertService,
        ILogger<OrganizationService> logger)
    {
        _repository = repository;
        _auditService = auditService;
        _publisher = publisher;
        _ledgerService = ledgerService;
        _identityService = identityService;
        _currentUserService = currentUserService;
        _alertService = alertService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<OrganizationDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var summaries = await _repository.GetAllWithStatsAsync(cancellationToken);
        
        return summaries.Select(s => MapToDto(
            s.Org, 
            s.UserCount, 
            s.CeoName, 
            s.OrgBalance + s.UserBalanceSum, 
            s.UserBalanceSum));
    }

    /// <inheritdoc />
    public async Task<OrganizationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var org = await _repository.GetByIdAsync(id, cancellationToken);
        if (org == null) return null;

        var userCount = await _repository.GetUserCountAsync(org.Id, cancellationToken);
        var orgBalance = await _repository.GetOrganizationBalanceAsync(org.Id, cancellationToken);
        var totalUsage = await _repository.GetTotalUserBalanceAsync(org.Id, cancellationToken);
        
        string? ceoName = await ResolveCeoNameAsync(org, cancellationToken);

        return MapToDto(org, userCount, ceoName, orgBalance + totalUsage, totalUsage);
    }

    /// <inheritdoc />
    public async Task<OrganizationDto> CreateAsync(CreateOrganizationDto dto, CancellationToken cancellationToken = default)
    {
        // Business Rule: Enforce unique organization names within the system.
        if (await _repository.IsNameTakenAsync(dto.Name, null, cancellationToken))


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
        await _repository.SaveChangesAsync(cancellationToken);

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
        if (await _repository.IsNameTakenAsync(dto.Name, dto.Id, cancellationToken))
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
                await _identityService.SyncLeadingRoleAsync(newCeoId, FMC.Shared.Auth.Roles.CEO, cancellationToken);
            }

            // 2. (Optional) You might want to demote the old CEO to 'User' or 'Manager', 
            // but for now we'll prioritize making sure the new one is correctly set.
        }

        _repository.Update(org); // stamps UpdatedAt automatically
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[OrganizationService] Updated organization Id: {Id} and synchronized leadership roles.", org.Id);
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
        await _repository.SaveChangesAsync(cancellationToken);

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
        var account = await _repository.GetAccountByTenantIdAsync(tenantId, cancellationToken);

        if (account == null)
        {
            account = new Account
            {
                Id = Guid.NewGuid(),
                Name = "Core Operations Wallet",
                Balance = 0,
                TenantId = tenantId,
                OrganizationId = organizationId
            };
            await _repository.AddAccountAsync(account, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        // 3. Operational Ledger Execution
        if (amount > 0)
        {
            await _ledgerService.CreditAsync(account.Id, amount, cancellationToken);
        }
        else if (amount < 0)
        {
            await _ledgerService.DebitAsync(account.Id, Math.Abs(amount), cancellationToken);
        }

        // Fetch new balance for notification/alerts
        var newBalance = await _ledgerService.GetBalanceAsync(account.Id, cancellationToken);

        // 4. Persist Transaction History (System adjustments are auto-completed)
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Label = label ?? (amount >= 0 ? "System Credit" : "System Debit"),
            Amount = amount,
            Date = DateTime.UtcNow,
            Category = "System Adjustment",
            TenantId = tenantId,
            AccountId = account.Id,
            Status = "Completed",
            MakerId = performedBy,
            OrganizationId = organizationId
        };
        await _repository.AddTransactionAsync(transaction, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        // 5. Automated Intelligence: Raise alert for negative equity
        if (newBalance < 0)
        {
            await _alertService.RaiseAlertAsync(
                "Negative Ledger Balance", 
                $"Tenant '{org.Name}' is operating with negative liquidity ({newBalance:C}). Immediate settlement or suspension recommended.", 
                AlertSeverity.Warning, 
                organizationId.ToString(), 
                "Organization"
            );
        }

        // 6. Audit Trail & Notifications
        var performedByEmail = (await _identityService.GetUserByIdAsync(performedBy))?.Email;
        await _auditService.RecordFinancialEventAsync(
            amount > 0 ? "WALLET_FUNDED" : "WALLET_DEBITED", 
            org.Id, 
            "N/A", 
            Math.Abs(amount), 
            label ?? "Administrative Adjustment", 
            performedBy, 
            performedByEmail);

        await _publisher.Publish(new WalletAdjustedEvent(org.Id, amount, newBalance, label ?? "Administrative Adjustment"), cancellationToken);

        _logger.LogInformation("[OrganizationService] Successfully adjusted balance for Tenant {Id} by {Amount}. New Balance: {NewBalance}", 
            org.Id, amount, newBalance);

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
        var userDto = await _identityService.GetUserByIdAsync(userId.ToString());
        if (userDto == null) return false;

        // 3. Structural Isolation: Only 'User' roles (Cardholders) hold wallets
        if (userDto.Role != FMC.Shared.Auth.Roles.User)
        {
            _logger.LogWarning("[OrganizationService] Invalid Target: User {UserId} is a Staff Credential and cannot receive wallet allotments.", userId);
            return false;
        }

        // 4. Identify the personal wallet account
        var tenantId = userDto.Id;
        var account = await _repository.GetAccountByTenantIdAsync(tenantId, cancellationToken);

        if (account == null)
        {
            account = new Account
            {
                Id = Guid.NewGuid(),
                Name = $"Wallet: {userDto.FirstName} {userDto.LastName}",
                Balance = 0,
                TenantId = tenantId,
                OrganizationId = userDto.OrganizationId
            };
            await _repository.AddAccountAsync(account, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
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
            OrganizationId = userDto.OrganizationId ?? _currentUserService.OrganizationId
        };
        
        await _repository.AddTransactionAsync(transaction, cancellationToken);

        // 4. Trace the forensic audit event for Initiation
        await _auditService.RecordFinancialEventAsync(
            "INITIATE_" + (amount >= 0 ? "CREDIT" : "DEBIT"), 
            userDto.OrganizationId ?? Guid.Empty, 
            userDto.DisplayName, 
            Math.Abs(amount), 
            label ?? "Pending Allotment Initiation", 
            performedBy,
            null,
            tenantId
        );

        await _repository.SaveChangesAsync(cancellationToken);
        
        // ── Phase 1: Workflow Notifications (Handled by OrganizationNotificationHandler) ──
        if (userDto.OrganizationId.HasValue)
        {
            await _publisher.Publish(new TransactionPendingEvent(userDto.OrganizationId.Value, performedBy, userId.ToString(), amount, label ?? "Standard Allotment"), cancellationToken);
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

        var transaction = await _repository.GetTransactionByIdAsync(transactionId, cancellationToken);
        if (transaction == null || transaction.Status != "Pending") throw new ApplicationException("Transaction not found or already processed.");

        // 2. Mutual Exclusion (4-Eyes Principle): Maker cannot be Approver
        if (transaction.MakerId == approverId)
        {
            await _auditService.RecordFinancialEventAsync("APPROVAL_BLOCKED", transaction.OrganizationId ?? Guid.Empty, "N/A", transaction.Amount, "4-Eyes Violation: Maker tried to approve own request.", approverId, null, transaction.TenantId);
            throw new ApplicationException("Four-Eyes Policy: You cannot approve transactions you initiated.");
        }

        // 3. Organization Isolation: Approver must belong to the same org
        var approver = await _identityService.GetUserByIdAsync(approverId);
        bool txHasOrg = transaction.OrganizationId.HasValue && transaction.OrganizationId != Guid.Empty;
        bool sameOrg = !txHasOrg || (approver != null && approver.OrganizationId == transaction.OrganizationId);
        
        if (approver == null || (!sameOrg && !_currentUserService.IsSuperAdmin))
        {
            await _auditService.RecordFinancialEventAsync("APPROVAL_BLOCKED", transaction.OrganizationId ?? Guid.Empty, "N/A", transaction.Amount, "Residency Violation: Approver belongs to different organization.", approverId, null, transaction.TenantId);
            throw new ApplicationException("Organizational Security: You can only approve transactions for your own organization.");
        }

        // 4. Identify Target Account (Cardholder Account)
        var userAccount = await _repository.GetAccountByIdAsync(transaction.AccountId);
        if (userAccount == null) throw new ApplicationException("Target cardholder account not found.");

        // 5. Identify Source Account (Organization Operations Wallet)
        if (!transaction.OrganizationId.HasValue) throw new ApplicationException("Transaction Security: Organization context is missing.");
        
        var orgTenantId = transaction.OrganizationId.Value.ToString();
        var orgAccount = await _repository.GetAccountByTenantIdAsync(orgTenantId, cancellationToken);

        if (orgAccount == null) throw new ApplicationException("Ledger Error: Organization operations wallet not found.");

        // 5. Execute Atomic Transfer
        await _ledgerService.TransferAsync(orgAccount.Id, userAccount.Id, transaction.Amount, cancellationToken);

        // 6. Update Transaction Status
        transaction.Status = "Approved";
        transaction.ApproverId = approverId;
        transaction.ActionDate = DateTime.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken);

        // 7. Audit Trail for Final Commitment
        await _auditService.RecordFinancialEventAsync(
            "TRANSACTION_APPROVED", 
            transaction.OrganizationId.Value, 
            userAccount.Name, 
            transaction.Amount, 
            transaction.Label, 
            approverId, 
            approver?.Email, 
            transaction.TenantId);

        // ── Phase 2: Workflow Completion Notifications (Handled by OrganizationNotificationHandler) ──
        if (transaction.OrganizationId.HasValue)
        {
            await _publisher.Publish(new TransactionApprovedEvent(transaction.OrganizationId.Value, transaction.Id), cancellationToken);
        }

        _logger.LogInformation("[OrganizationService] Transaction {Id} APPROVED by {ApproverId}. Amount settled.", 
            transactionId, approverId);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RejectTransactionAsync(Guid transactionId, string approverId, string reason, CancellationToken cancellationToken = default)
    {
        // 1. Security Check: Only Approver can reject
        if (!_currentUserService.IsApprover) throw new ApplicationException("Access Denied: You do not have the Approver role.");

        var transaction = await _repository.GetTransactionByIdAsync(transactionId, cancellationToken);
        if (transaction == null || transaction.Status != "Pending") return false;

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

        await _repository.SaveChangesAsync(cancellationToken);
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

        var transactions = await _repository.GetTransactionsByStatusAsync(organizationId, "Pending", cancellationToken);

        var result = new List<FMC.Shared.DTOs.TransactionDto>();
        foreach(var t in transactions)
        {
            var account = await _repository.GetAccountByIdAsync(t.AccountId, cancellationToken);
            var makerAttribute = !string.IsNullOrEmpty(t.MakerId) ? await _identityService.GetUserByIdAsync(t.MakerId) : null;
            
            // Resolve Subscriber details (User or Org)
            string subscriberName = "N/A";
            string? accountNumber = null;

            if (account != null)
            {
                subscriberName = account.Name.Replace("Wallet: ", "");
                var user = await _identityService.GetUserByIdAsync(account.TenantId ?? "");
                if (user != null)
                {
                    accountNumber = user.AccountNumber;
                }
                else if (Guid.TryParse(account.TenantId, out var orgId))
                {
                    var org = await _repository.GetByIdAsync(orgId, cancellationToken);
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
                Status = t.Status,
                Subscriber = subscriberName,
                AccountNumber = accountNumber,
                MakerName = makerAttribute?.DisplayName ?? "System",
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
        var transactions = await _repository.GetTransactionsByDateAsync(organizationId, today, cancellationToken);

        var result = new List<FMC.Shared.DTOs.TransactionDto>();
        foreach(var t in transactions)
        {
            var account = await _repository.GetAccountByIdAsync(t.AccountId, cancellationToken);
            var makerAttribute = !string.IsNullOrEmpty(t.MakerId) ? await _identityService.GetUserByIdAsync(t.MakerId) : null;
            var approver = !string.IsNullOrEmpty(t.ApproverId) ? await _identityService.GetUserByIdAsync(t.ApproverId) : null;
            
            // Resolve Subscriber details (User or Org)
            string subscriberName = "N/A";
            string? accountNumber = null;

            if (account != null)
            {
                subscriberName = account.Name.Replace("Wallet: ", "");
                var user = await _identityService.GetUserByIdAsync(account.TenantId ?? "");
                if (user != null)
                {
                    accountNumber = user.AccountNumber;
                }
                else if (Guid.TryParse(account.TenantId, out var orgId))
                {
                    var org = await _repository.GetByIdAsync(orgId, cancellationToken);
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
                Status = t.Status,
                Subscriber = subscriberName,
                AccountNumber = accountNumber,
                MakerName = makerAttribute?.DisplayName ?? "System",
                MakerId = t.MakerId,
                ApproverName = approver?.DisplayName,
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
        if (string.IsNullOrEmpty(org.ChiefExecutiveId)) return null;
        var user = await _identityService.GetUserByIdAsync(org.ChiefExecutiveId);
        return user?.DisplayName;
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
        var transaction = await _repository.GetTransactionByIdAsync(transactionId, cancellationToken);

        if (transaction == null || transaction.Status != "Pending") return false;

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

        await _repository.SaveChangesAsync(cancellationToken);
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

        var transactions = await _repository.GetOrganizationTransactionsAsync(organizationId, status, count, cancellationToken);

        var result = new List<FMC.Shared.DTOs.TransactionDto>();
        foreach (var t in transactions)
        {
            var account = await _repository.GetAccountByIdAsync(t.AccountId, cancellationToken);
            var maker = !string.IsNullOrEmpty(t.MakerId) ? await _identityService.GetUserByIdAsync(t.MakerId) : null;
            var approver = !string.IsNullOrEmpty(t.ApproverId) ? await _identityService.GetUserByIdAsync(t.ApproverId) : null;

            string subscriberName = "System Node";
            string? accountNumber = null;

            if (account != null)
            {
                subscriberName = account.Name.Replace("Wallet: ", "");
                var user = await _identityService.GetUserByIdAsync(account.TenantId ?? "");
                if (user != null)
                {
                    accountNumber = user.AccountNumber;
                }
                else
                {
                    if (Guid.TryParse(account.TenantId, out var orgId))
                    {
                        var org = await _repository.GetByIdAsync(orgId, cancellationToken);
                        if (org != null) accountNumber = org.AccountNumber;
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
                Status = string.IsNullOrWhiteSpace(t.Status) ? "Successful" : t.Status,
                Subscriber = subscriberName,
                AccountNumber = accountNumber,
                MakerName = maker?.DisplayName ?? "System",
                MakerId = t.MakerId,
                ApproverName = approver?.DisplayName,
                ActionDate = t.ActionDate,
                OrganizationId = t.OrganizationId
            });
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FMC.Shared.DTOs.Admin.SystemAlertDto>> GetWorkflowAlertsAsync(Guid organizationId, string userId, string role, CancellationToken cancellationToken = default)
    {
        try 
        {
            var alerts = new List<FMC.Shared.DTOs.Admin.SystemAlertDto>();
            var since = DateTime.UtcNow.AddDays(-1);

            bool isCeo = role.Equals(FMC.Shared.Auth.Roles.CEO, StringComparison.OrdinalIgnoreCase);
            bool isApprover = role.Equals(FMC.Shared.Auth.Roles.Approver, StringComparison.OrdinalIgnoreCase);
            bool isMaker = role.Equals(FMC.Shared.Auth.Roles.Maker, StringComparison.OrdinalIgnoreCase);

            if (isApprover || isCeo)
            {
                var txs = await _repository.GetTransactionsByStatusAsync(organizationId, "Pending", cancellationToken);
                var pCount = txs.Count();
                if (pCount > 0) 
                {
                    var lastPendingDate = txs.Max(t => t.Date).Ticks;
                    alerts.Add(CreateAlert("Pending Validations", $"{pCount} request(s) waiting to be approved.", $"Pending_{organizationId}_{lastPendingDate}", FMC.Shared.DTOs.Admin.AlertSeverityDto.Warning));
                }
            }

            if (isMaker || isCeo)
            {
                var processed = await _repository.GetProcessedTransactionsSinceAsync(organizationId, since, cancellationToken);
                var q = processed.Where(t => t.Status == "Approved");
                
                if (isMaker && !isCeo) q = q.Where(t => t.MakerId == userId);
                
                var aCount = q.Count();
                if (aCount > 0) 
                {
                    var lastActionDate = q.Max(t => t.ActionDate ?? DateTime.MinValue).Ticks;
                    alerts.Add(CreateAlert("Approved Requests", $"{aCount} request(s) recently approved.", $"Processed_{organizationId}_{lastActionDate}", FMC.Shared.DTOs.Admin.AlertSeverityDto.Information));
                }
            }

            if (isMaker || isCeo || isApprover)
            {
                var orgBalance = await _repository.GetOrganizationBalanceAsync(organizationId, cancellationToken);
                var userBalanceSum = await _repository.GetTotalUserBalanceAsync(organizationId, cancellationToken);

                var total = orgBalance + userBalanceSum;
                var usedPct = total > 0 ? (userBalanceSum / total) * 100m : 0m;

                if (usedPct >= 80m || orgBalance <= 100_000m)
                {
                    var msg = usedPct >= 80m ? $"{usedPct:F1}% of wallet allocated." : $"Only {orgBalance:C} remaining in org wallet.";
                    var sev = usedPct >= 80m ? FMC.Shared.DTOs.Admin.AlertSeverityDto.Security : FMC.Shared.DTOs.Admin.AlertSeverityDto.Warning;
                    alerts.Add(CreateAlert("Capacity Threshold", msg, $"Threshold_{organizationId}_{(int)usedPct}", sev));
                }
            }

            return alerts;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[OrganizationService] Workflow alert generation canceled by request abort.");
            return Enumerable.Empty<FMC.Shared.DTOs.Admin.SystemAlertDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OrganizationService] Failed to generate workflow alerts for Org {OrgId}", organizationId);
            return Enumerable.Empty<FMC.Shared.DTOs.Admin.SystemAlertDto>();
        }
    }



    FMC.Shared.DTOs.Admin.SystemAlertDto CreateAlert(string title, string msg, string entityId, FMC.Shared.DTOs.Admin.AlertSeverityDto sev) => new()
    {
        Id = 0, Title = title, Message = msg, Severity = sev, EntityType = "Workflow", EntityId = entityId, CreatedAt = DateTime.UtcNow
    };
}
