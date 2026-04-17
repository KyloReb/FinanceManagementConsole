using FMC.Application.Interfaces;
using FMC.Application.Organizations.Events;
using FMC.Domain.Entities;
using FMC.Infrastructure.Data;
using FMC.Shared.Utils;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FMC.Infrastructure.Services;

public class OrganizationNotificationHandler : 
    INotificationHandler<TransactionPendingEvent>,
    INotificationHandler<TransactionApprovedEvent>,
    INotificationHandler<WalletAdjustedEvent>
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _templateService;
    private readonly ILogger<OrganizationNotificationHandler> _logger;

    public OrganizationNotificationHandler(
        ApplicationDbContext context,
        IEmailService emailService,
        IEmailTemplateService templateService,
        ILogger<OrganizationNotificationHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _templateService = templateService;
        _logger = logger;
    }

    public async Task Handle(TransactionPendingEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var org = await _context.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == notification.OrganizationId, cancellationToken);
            var user = await _context.Users.OfType<ApplicationUser>().IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == notification.TargetUserId, cancellationToken);
            
            if (org != null && user != null)
            {
                var approverEmails = await (from u in _context.Users.OfType<ApplicationUser>()
                                            where u.OrganizationId == org.Id
                                            join ur in _context.UserRoles on u.Id equals ur.UserId
                                            join r in _context.Roles on ur.RoleId equals r.Id
                                            where r.Name == FMC.Shared.Auth.Roles.Approver
                                            select u.Email).ToListAsync(cancellationToken);

                string? ceoEmail = await GetCeoEmail(org.ChiefExecutiveId, cancellationToken);

                var recipients = approverEmails.ToHashSet();
                if (!string.IsNullOrEmpty(ceoEmail)) recipients.Add(ceoEmail);

                if (recipients.Any())
                {
                    var logoBytes = Convert.FromBase64String(FMC.Infrastructure.Authentication.BrandingConstants.NationlinkLogoBase64);
                    var attachments = new Dictionary<string, byte[]> { { "nlklogo", logoBytes } };
                    
                    var body = _templateService.GeneratePendingApprovalEmail(
                        org.Name, 
                        notification.MakerName, 
                        $"{user.FirstName} {user.LastName}", 
                        FinanceUtils.MaskCard(user.AccountNumber), 
                        notification.Amount);

                    foreach (var email in recipients)
                    {
                        await _emailService.SendEmailAsync(email, $"FMC Action Required: Pending Approval for {org.Name}", body, attachments);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle TransactionPendingEvent.");
        }
    }

    public async Task Handle(TransactionApprovedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var transaction = await _context.Transactions.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == notification.TransactionId, cancellationToken);
            var org = await _context.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == notification.OrganizationId, cancellationToken);
            
            if (transaction != null && org != null)
            {
                var targetUser = await _context.Users.OfType<ApplicationUser>().IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == transaction.TenantId, cancellationToken);
                var makerEmail = await _context.Users.IgnoreQueryFilters().Where(u => u.Id == transaction.MakerId).Select(u => u.Email).FirstOrDefaultAsync(cancellationToken);
                var ceoEmail = await GetCeoEmail(org.ChiefExecutiveId, cancellationToken);

                var recipients = new HashSet<string>();
                if (!string.IsNullOrEmpty(makerEmail)) recipients.Add(makerEmail);
                if (!string.IsNullOrEmpty(ceoEmail)) recipients.Add(ceoEmail);

                if (recipients.Any())
                {
                    var logoBytes = Convert.FromBase64String(FMC.Infrastructure.Authentication.BrandingConstants.NationlinkLogoBase64);
                    var attachments = new Dictionary<string, byte[]> { { "nlklogo", logoBytes } };
                    
                    var targetName = targetUser != null ? $"{targetUser.FirstName} {targetUser.LastName}" : "Subscriber";
                    var targetCard = targetUser?.AccountNumber ?? "N/A";

                    var body = _templateService.GenerateTransactionApprovedEmail(org.Name, targetName, FinanceUtils.MaskCard(targetCard), transaction.Amount);
                    
                    // Also handle capacity alerts here if needed, or in a separate handler
                    // For Phase 2, we just move the approval email. 
                    // Let's check capacity separately to keep it clean.
                    
                    foreach (var email in recipients)
                    {
                        await _emailService.SendEmailAsync(email, $"FMC Notification: Transaction Approved — {org.Name}", body, attachments);
                    }
                }
                
                // Trigger Capacity Alert if needed (moved from service)
                await HandleCapacityAlert(org, transaction.Amount, ceoEmail, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle TransactionApprovedEvent.");
        }
    }

    public async Task Handle(WalletAdjustedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var org = await _context.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == notification.OrganizationId, cancellationToken);
            if (org != null)
            {
                string? ceoEmail = await GetCeoEmail(org.ChiefExecutiveId, cancellationToken);
                if (!string.IsNullOrEmpty(ceoEmail))
                {
                    var isCredit = notification.Amount > 0;
                    var logoBytes = Convert.FromBase64String(FMC.Infrastructure.Authentication.BrandingConstants.NationlinkLogoBase64);
                    var attachments = new Dictionary<string, byte[]> { { "nlklogo", logoBytes } };
                    
                    var body = _templateService.GenerateWalletAdjustmentEmail(
                        org.Name, 
                        FinanceUtils.MaskCard(org.AccountNumber), 
                        notification.Amount, 
                        notification.NewBalance, 
                        isCredit);

                    var actionTitle = isCredit ? "Wallet Credited Successfully" : "Wallet Adjustment (Debit)";
                    await _emailService.SendEmailAsync(ceoEmail, $"FMC Advisory: {actionTitle} — {org.Name}", body, attachments);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle WalletAdjustedEvent.");
        }
    }

    private async Task HandleCapacityAlert(Organization org, decimal lastTxAmount, string? ceoEmail, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(ceoEmail)) return;

        var orgTenantId = org.Id.ToString();
        var orgBalance = await _context.Accounts.IgnoreQueryFilters()
            .Where(a => a.TenantId == orgTenantId)
            .Select(a => a.Balance)
            .FirstOrDefaultAsync(cancellationToken);

        var userBalanceSum = await (from a in _context.Accounts.IgnoreQueryFilters()
                                          where a.OrganizationId == org.Id && a.TenantId != orgTenantId
                                          select a.Balance).SumAsync(cancellationToken);

        var total = orgBalance + userBalanceSum;
        var usedPct = total > 0 ? (userBalanceSum / total) * 100m : 0m;

        if (usedPct >= 80m || orgBalance <= 100_000m)
        {
            var alertType = usedPct >= 80m ? $"{usedPct:F0}% Operational Capacity Alert" : "Critical Liquidity Advisory";
            var body = _templateService.GenerateCapacityThresholdEmail(org.Name, total, userBalanceSum, usedPct, orgBalance);
            
            var logoBytes = Convert.FromBase64String(FMC.Infrastructure.Authentication.BrandingConstants.NationlinkLogoBase64);
            var attachments = new Dictionary<string, byte[]> { { "nlklogo", logoBytes } };
            
            await _emailService.SendEmailAsync(ceoEmail, $"FMC Advisory: {alertType} — {org.Name}", body, attachments);
        }
    }

    private async Task<string?> GetCeoEmail(string? ceoId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(ceoId)) return null;
        return await _context.Users.IgnoreQueryFilters()
            .Where(u => u.Id == ceoId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
