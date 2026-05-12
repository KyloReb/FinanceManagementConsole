using FMC.Application.Interfaces;
using FMC.Application.Organizations.Events;
using FMC.Domain.Entities;
using FMC.Infrastructure.Data;
using FMC.Shared.DTOs.Organization;
using FMC.Shared.DTOs.User;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;

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
    public async Task<UserDto?> GetUserByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        // 1. Try Identity Service first (Staff)
        var staff = await _identityService.GetUserByIdAsync(id);
        if (staff != null) return staff;

        // 2. Try Cardholders Table
        if (Guid.TryParse(id, out var cardholderId))
        {
            var cardholder = await _repository.GetCardholderByIdAsync(cardholderId, cancellationToken);
            if (cardholder != null)
            {
                var account = await _repository.GetAccountByTenantIdAsync(cardholder.Id.ToString(), cancellationToken);
                return new UserDto
                {
                    Id = cardholder.Id.ToString(),
                    Email = cardholder.Email,
                    FirstName = cardholder.FirstName,
                    LastName = cardholder.LastName,
                    IsActive = cardholder.IsActive,
                    OrganizationId = cardholder.OrganizationId,
                    Organization = cardholder.Organization?.Name,
                    AccountNumber = cardholder.AccountNumber,
                    Balance = account?.Balance ?? 0,
                    Role = FMC.Shared.Auth.Roles.User,
                    CreatedAt = cardholder.CreatedAt
                };
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        // 1. Fetch all Staff from Identity Service
        var staff = await _identityService.GetAllUsersAsync();

        // 2. Fetch all Cardholders from the dedicated Cardholders table
        var cardholders = await _repository.GetAllCardholdersAsync(cancellationToken);

        // 3. Map Cardholders to UserDto
        var cardholderDtos = new List<UserDto>();
        foreach (var c in cardholders)
        {
            var account = await _repository.GetAccountByTenantIdAsync(c.Id.ToString(), cancellationToken);

            cardholderDtos.Add(new UserDto
            {
                Id = c.Id.ToString(),
                Email = c.Email,
                FirstName = c.FirstName,
                LastName = c.LastName,
                IsActive = c.IsActive,
                OrganizationId = c.OrganizationId,
                Organization = c.Organization?.Name,
                AccountNumber = c.AccountNumber,
                Balance = account?.Balance ?? 0,
                Role = FMC.Shared.Auth.Roles.User,
                CreatedAt = c.CreatedAt
            });
        }

        // 4. Merge and return
        return staff.Concat(cardholderDtos);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FMC.Shared.DTOs.User.UserDto>> GetUsersByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        // 1. Fetch Staff from Identity Service (Maker, CEO, Approver)
        var staff = await _identityService.GetUsersByOrganizationAsync(organizationId);
        
        // 2. Fetch Cardholders from the dedicated Cardholders table
        var cardholders = await _repository.GetCardholdersByOrganizationAsync(organizationId, cancellationToken);
        
        // 3. Map Cardholders to UserDto
        var cardholderDtos = new List<UserDto>();
        foreach (var c in cardholders)
        {
            // For cardholders, we fetch balance from their dedicated account
            var balance = await _repository.GetOrganizationBalanceAsync(c.Id, cancellationToken); // Note: reusing GetOrganizationBalanceAsync which uses TenantId internally
            
            // Actually, for cardholders, the Account.TenantId is their Id.ToString()
            var account = await _repository.GetAccountByTenantIdAsync(c.Id.ToString(), cancellationToken);

            cardholderDtos.Add(new UserDto
            {
                Id = c.Id.ToString(),
                Email = c.Email,
                FirstName = c.FirstName,
                LastName = c.LastName,
                IsActive = c.IsActive,
                OrganizationId = c.OrganizationId,
                Organization = c.Organization?.Name,
                AccountNumber = c.AccountNumber,
                Balance = account?.Balance ?? 0,
                Role = FMC.Shared.Auth.Roles.User,
                CreatedAt = c.CreatedAt
            });
        }

        // 4. Merge and return
        return staff.Concat(cardholderDtos);
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
            await _ledgerService.CreditAsync(account.Id, amount, cancellationToken: cancellationToken);
        }
        else if (amount < 0)
        {
            await _ledgerService.DebitAsync(account.Id, Math.Abs(amount), cancellationToken: cancellationToken);
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
    public async Task<bool> AdjustUserBalanceAsync(Guid userId, decimal amount, string label, string performedBy, string? idempotencyKey = null, Guid? parentTransactionId = null, bool isSettlement = false, CancellationToken cancellationToken = default)
    {
        // 1. Security Check: Only Maker can initiate. SuperAdmins focus on Org-level funding.
        if (!_currentUserService.IsMaker || _currentUserService.IsSuperAdmin)
        {
            _logger.LogWarning("[OrganizationService] Access Denied: User {UserId} (Maker:{IsMaker}, SuperAdmin:{IsAdmin}) tried to initiate cardholder transaction.", 
                _currentUserService.UserId, _currentUserService.IsMaker, _currentUserService.IsSuperAdmin);
            return false; 
        }

        // 2. Identify the target user (Check Cardholders first, then Identity)
        string firstName, lastName, tenantId;
        Guid? orgId;

        var cardholder = await _repository.GetCardholderByIdAsync(userId, cancellationToken);
        if (cardholder != null)
        {
            firstName = cardholder.FirstName;
            lastName = cardholder.LastName;
            tenantId = cardholder.Id.ToString();
            orgId = cardholder.OrganizationId;
        }
        else
        {
            var userDto = await _identityService.GetUserByIdAsync(userId.ToString());
            if (userDto == null) return false;

            // Structural Isolation: Only 'User' roles (Cardholders) hold wallets
            if (userDto.Role != FMC.Shared.Auth.Roles.User)
            {
                _logger.LogWarning("[OrganizationService] Invalid Target: User {UserId} is a Staff Credential and cannot receive wallet allotments.", userId);
                return false;
            }

            firstName = userDto.FirstName ?? "Subscriber";
            lastName = userDto.LastName ?? "";
            tenantId = userDto.Id;
            orgId = userDto.OrganizationId;
        }

        // 4. Identify the personal wallet account
        var account = await _repository.GetAccountByTenantIdAsync(tenantId, cancellationToken);

        if (account == null)
        {
            account = new Account
            {
                Id = Guid.NewGuid(),
                Name = $"Wallet: {firstName} {lastName}",
                Balance = 0,
                TenantId = tenantId,
                OrganizationId = orgId
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
            OrganizationId = orgId ?? _currentUserService.OrganizationId
        };
        
        await _repository.AddTransactionAsync(transaction, cancellationToken);

        // 4. Trace the forensic audit event for Initiation
        await _auditService.RecordFinancialEventAsync(
            "INITIATE_" + (amount >= 0 ? "CREDIT" : "DEBIT"), 
            orgId ?? Guid.Empty, 
            $"{firstName} {lastName}".Trim(), 
            Math.Abs(amount), 
            label ?? "Pending Allotment Initiation", 
            performedBy,
            null,
            orgId?.ToString()
        );

        await _repository.SaveChangesAsync(cancellationToken);
        
        // ── Phase 1: Workflow Notifications (Handled by OrganizationNotificationHandler) ──
        if (orgId.HasValue)
        {
            await _publisher.Publish(new TransactionPendingEvent(orgId.Value, performedBy, userId.ToString(), amount, label ?? "Standard Allotment"), cancellationToken);
        }
        
        _logger.LogInformation("[OrganizationService] Transaction PENDING for {UserId} by Maker {MakerId}. Amount: {Amount}", 
            userId, _currentUserService.UserId, amount);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ApproveTransactionAsync(Guid transactionId, string approverId, bool publishEvent = true, bool skipAuditLog = false, CancellationToken cancellationToken = default)
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
        await _ledgerService.TransferAsync(orgAccount.Id, userAccount.Id, transaction.Amount, cancellationToken: cancellationToken);

        // 6. Update Transaction Status
        transaction.Status = "Approved";
        transaction.ApproverId = approverId;
        transaction.ActionDate = DateTime.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken);

        // 7. Audit Trail for Final Commitment
        if (!skipAuditLog)
        {
            await _auditService.RecordFinancialEventAsync(
                "TRANSACTION_APPROVED", 
                transaction.OrganizationId.Value, 
                userAccount.Name, 
                transaction.Amount, 
                transaction.Label, 
                approverId, 
                approver?.Email, 
                transaction.OrganizationId.Value.ToString());
        }

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
    public async Task<bool> RejectTransactionAsync(Guid transactionId, string approverId, string reason, bool skipAuditLog = false, CancellationToken cancellationToken = default)
    {
        // 1. Security Check: Only Approver can reject
        if (!_currentUserService.IsApprover) throw new ApplicationException("Access Denied: You do not have the Approver role.");

        var transaction = await _repository.GetTransactionByIdAsync(transactionId, cancellationToken);
        if (transaction == null || transaction.Status != "Pending") return false;

        transaction.Status = "Rejected";
        transaction.ApproverId = approverId;
        transaction.ActionDate = DateTime.UtcNow;
        transaction.RejectionReason = reason;

        if (!skipAuditLog)
        {
            await _auditService.RecordFinancialEventAsync(
                "REJECTED", 
                transaction.OrganizationId ?? Guid.Empty, 
                "N/A", 
                Math.Abs(transaction.Amount), 
                $"Transaction Rejected: {reason}", 
                approverId,
                null,
                transaction.OrganizationId?.ToString()
            );
        }

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
                
                if (Guid.TryParse(account.TenantId, out var id))
                {
                    var cardholder = await _repository.GetCardholderByIdAsync(id, cancellationToken);
                    if (cardholder != null)
                    {
                        accountNumber = cardholder.AccountNumber;
                    }
                    else
                    {
                        var org = await _repository.GetByIdAsync(id, cancellationToken);
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
                Status = t.Status,
                Subscriber = subscriberName,
                AccountNumber = accountNumber,
                MakerName = makerAttribute?.DisplayName ?? "System",
                MakerId = t.MakerId,
                BatchId = t.BatchId
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
                
                if (Guid.TryParse(account.TenantId, out var id))
                {
                    var cardholder = await _repository.GetCardholderByIdAsync(id, cancellationToken);
                    if (cardholder != null)
                    {
                        accountNumber = cardholder.AccountNumber;
                    }
                    else
                    {
                        var org = await _repository.GetByIdAsync(id, cancellationToken);
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
                Status = t.Status,
                Subscriber = subscriberName,
                AccountNumber = accountNumber,
                MakerName = makerAttribute?.DisplayName ?? "System",
                MakerId = t.MakerId,
                ApproverName = approver?.DisplayName,
                ActionDate = t.ActionDate,
                BatchId = t.BatchId
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
            transaction.OrganizationId?.ToString()
        );

        await _repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> CancelBatchAsync(Guid batchId, string makerId, CancellationToken cancellationToken = default)
    {
        var transactions = await _repository.GetTransactionsByBatchIdAsync(batchId, cancellationToken);
        var pendingTransactions = transactions.Where(t => t.Status == "Pending").ToList();

        if (!pendingTransactions.Any()) return false;

        decimal totalAmount = 0;
        var orgId = pendingTransactions.First().OrganizationId ?? Guid.Empty;

        foreach (var tx in pendingTransactions)
        {
            // Only the original Maker can cancel their own pending request
            if (tx.MakerId != makerId) continue;

            tx.Status = "Cancelled";
            tx.ActionDate = DateTime.UtcNow;
            totalAmount += tx.Amount;
        }

        await _auditService.RecordFinancialEventAsync(
            "BATCH_CANCELLED", 
            orgId, 
            $"{pendingTransactions.Count} Cardholders (Bulk)", 
            Math.Abs(totalAmount), 
            "Batch Cancelled by Maker", 
            makerId,
            null,
            orgId.ToString()
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
                
                if (Guid.TryParse(account.TenantId, out var id))
                {
                    var cardholder = await _repository.GetCardholderByIdAsync(id, cancellationToken);
                    if (cardholder != null)
                    {
                        accountNumber = cardholder.AccountNumber;
                    }
                    else
                    {
                        var org = await _repository.GetByIdAsync(id, cancellationToken);
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
                OrganizationId = t.OrganizationId,
                BatchId = t.BatchId
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
            bool isSuperAdmin = role.Equals(FMC.Shared.Auth.Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase);

            _logger.LogInformation("[Workflow-Alert-Trace] Initiating alert scan. User: {User}, InitialOrg: {Org}, Role: {Role}", userId, organizationId, role);

            // Fallback: If organizationId is empty, try to resolve it from the user's record
            if (organizationId == Guid.Empty && !isSuperAdmin)
            {
                // Try searching by userId as ID first, then as name if needed
                var appUser = await _identityService.GetUserByIdAsync(userId);
                
                if (appUser == null)
                {
                    // If GetUserByIdAsync failed, the ID might be a username
                    var allUsers = await _identityService.GetAllUsersAsync();
                    appUser = allUsers.FirstOrDefault(u => u.UserName == userId || u.Email == userId);
                }

                if (appUser?.OrganizationId != null)
                {
                    organizationId = appUser.OrganizationId.Value;
                    _logger.LogInformation("[Workflow-Alert-Trace] Resolved OrgId {OrgId} from user profile for {User}", organizationId, userId);
                }
            }

            // 1. Pending Validations (For Makers, Approvers, CEOs, and SuperAdmins)
            if (isMaker || isApprover || isCeo || isSuperAdmin)
            {
                var txs = (await _repository.GetTransactionsByStatusAsync(organizationId, "Pending", cancellationToken)).ToList();

                if (txs.Any()) 
                {
                    var batches = txs.Where(t => t.BatchId.HasValue && t.BatchId != Guid.Empty).GroupBy(t => t.BatchId!.Value).ToList();
                    var individuals = txs.Where(t => !t.BatchId.HasValue || t.BatchId == Guid.Empty).ToList();

                    foreach (var batch in batches)
                    {
                        alerts.Add(CreateAlert(
                            "Bulk Batch Pending", 
                            $"A bulk allotment batch with {batch.Count()} item(s) requires your validation.", 
                            $"Pending_Batch_{batch.Key}", 
                            FMC.Shared.DTOs.Admin.AlertSeverityDto.Warning));
                    }

                    if (individuals.Any())
                    {
                        alerts.Add(CreateAlert("Pending Validations", $"{individuals.Count()} individual request(s) waiting.", $"Pending_Indiv_{organizationId}", FMC.Shared.DTOs.Admin.AlertSeverityDto.Warning));
                    }
                }
            }

            if (isMaker || isCeo || isSuperAdmin)
            {
                var processed = await _repository.GetProcessedTransactionsSinceAsync(organizationId, since, cancellationToken);
                var approved = processed.Where(t => t.Status == "Approved");
                if (isMaker && !isSuperAdmin && !isCeo) approved = approved.Where(t => t.MakerId == userId);

                if (approved.Any()) 
                {
                    alerts.Add(CreateAlert("Recent Approvals", $"{approved.Count()} requests settled.", $"Processed_{organizationId}", FMC.Shared.DTOs.Admin.AlertSeverityDto.Information));
                }
            }

            if (organizationId != Guid.Empty && (isMaker || isCeo || isApprover || isSuperAdmin))
            {
                // Security: Don't show capacity alerts for the system owner (Nationlink)
                var org = await _repository.GetByIdAsync(organizationId, cancellationToken);
                if (org != null && org.Name?.Contains("Nationlink", StringComparison.OrdinalIgnoreCase) == false)
                {
                    var orgBalance = await _repository.GetOrganizationBalanceAsync(organizationId, cancellationToken);
                    var userBalanceSum = await _repository.GetTotalUserBalanceAsync(organizationId, cancellationToken);

                    var total = orgBalance + userBalanceSum;
                    var usedPct = total > 0 ? (userBalanceSum / total) * 100m : 0m;

                    if (orgBalance <= 100_000m || usedPct >= 80m)
                    {
                        var msg = orgBalance <= 100_000m 
                            ? $"{org.Name} only has {orgBalance:C} remaining in org wallet." 
                            : $"{org.Name} has {usedPct:F1}% of wallet allocated.";
                            
                        var sev = (orgBalance <= 50_000m || usedPct >= 95m) ? FMC.Shared.DTOs.Admin.AlertSeverityDto.Security : FMC.Shared.DTOs.Admin.AlertSeverityDto.Warning;
                        alerts.Add(CreateAlert("Capacity Threshold", msg, organizationId.ToString(), sev));
                    }

                    // 4. Payroll Settlement Status (Specific to Makers, CEOs, and Approvers)
                    if (isMaker || isCeo || isApprover || isSuperAdmin)
                    {
                        var payrollThreshold = 250_000m;
                        var isHealthy = orgBalance >= payrollThreshold;
                        var msg = isHealthy 
                            ? $"Your corporate float of {orgBalance:C0} is sufficient for upcoming payroll operations."
                            : $"Corporate float is below safety threshold ({orgBalance:C0}). Settlement may be delayed.";
                        
                        alerts.Add(CreateAlert(
                            "Payroll Settlement Status", 
                            msg, 
                            $"Payroll_{organizationId}", 
                            isHealthy ? FMC.Shared.DTOs.Admin.AlertSeverityDto.Information : FMC.Shared.DTOs.Admin.AlertSeverityDto.Warning));
                    }

                    // 5. Organization Suspension Alert
                    if (!org.IsActive)
                    {
                        alerts.Add(CreateAlert(
                            "Organization Suspended", 
                            $"{org.Name} is currently in-active. Financial operations are restricted.", 
                            $"Status_{organizationId}", 
                            FMC.Shared.DTOs.Admin.AlertSeverityDto.Critical));
                    }
                }
            }

            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OrganizationService] Failed to generate workflow alerts for User {User}, Org {OrgId}", userId, organizationId);
            return Enumerable.Empty<FMC.Shared.DTOs.Admin.SystemAlertDto>();
        }
    }



    FMC.Shared.DTOs.Admin.SystemAlertDto CreateAlert(string title, string msg, string entityId, FMC.Shared.DTOs.Admin.AlertSeverityDto sev) => new()
    {
        Id = 0, Title = title, Message = msg, Severity = sev, EntityType = "Workflow", EntityId = entityId, CreatedAt = DateTime.UtcNow
    };

    /// <inheritdoc />
    public async Task<bool> ApproveBatchAsync(Guid batchId, string approverId, CancellationToken cancellationToken = default)
    {
        var transactions = await _repository.GetTransactionsByBatchIdAsync(batchId, cancellationToken);
        var pendingTransactions = transactions.Where(t => t.Status == "Pending").ToList();

        if (!pendingTransactions.Any()) return false;

        decimal totalAmount = 0;
        var orgId = pendingTransactions.First().OrganizationId ?? Guid.Empty;
        var tenantId = pendingTransactions.First().TenantId;
        var label = pendingTransactions.First().Label ?? "Bulk Batch Approval";

        foreach (var tx in pendingTransactions)
        {
            totalAmount += tx.Amount;
            // Reuse the existing single-transaction approval logic to ensure all security checks (4-eyes, org-isolation) are honored
            await ApproveTransactionAsync(tx.Id, approverId, true, skipAuditLog: true, cancellationToken);
        }

        var approver = await _identityService.GetUserByIdAsync(approverId);
        await _auditService.RecordFinancialEventAsync(
            "BATCH_APPROVED", 
            orgId, 
            $"{pendingTransactions.Count} Cardholders (Bulk)", 
            Math.Abs(totalAmount), 
            label, 
            approverId, 
            approver?.Email, 
            orgId.ToString());

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RejectBatchAsync(Guid batchId, string approverId, string reason, CancellationToken cancellationToken = default)
    {
        var transactions = await _repository.GetTransactionsByBatchIdAsync(batchId, cancellationToken);
        var pendingTransactions = transactions.Where(t => t.Status == "Pending").ToList();

        if (!pendingTransactions.Any()) return false;

        decimal totalAmount = 0;
        var orgId = pendingTransactions.First().OrganizationId ?? Guid.Empty;
        var tenantId = pendingTransactions.First().TenantId;

        foreach (var tx in pendingTransactions)
        {
            totalAmount += tx.Amount;
            await RejectTransactionAsync(tx.Id, approverId, reason, skipAuditLog: true, cancellationToken);
        }

        await _auditService.RecordFinancialEventAsync(
            "BATCH_REJECTED", 
            orgId, 
            $"{pendingTransactions.Count} Cardholders (Bulk)", 
            Math.Abs(totalAmount), 
            $"Batch Rejected: {reason}", 
            approverId, 
            null, 
            orgId.ToString());

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> SyncOrganizationLimitAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var org = await _repository.GetByIdAsync(organizationId, cancellationToken);
        if (org == null) return false;

        // Calculate total liquidity: Org Wallet Balance + Sum of all User Balances
        var orgBalance = await _repository.GetOrganizationBalanceAsync(organizationId, cancellationToken);
        var userBalanceSum = await _repository.GetTotalUserBalanceAsync(organizationId, cancellationToken);
        
        var totalLiquidity = orgBalance + userBalanceSum;

        org.WalletLimit = totalLiquidity;
        org.UpdatedAt = DateTime.UtcNow;

        _repository.Update(org);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[OrganizationService] Synchronized Wallet Limit for Org {OrgName} to {NewLimit:C}", org.Name, totalLiquidity);
        
        await _auditService.RecordFinancialEventAsync("SYNC_LIMIT", organizationId, "N/A", totalLiquidity, "Capacity Reset", "System", null, organizationId.ToString());
        
        return true;
    }
}
