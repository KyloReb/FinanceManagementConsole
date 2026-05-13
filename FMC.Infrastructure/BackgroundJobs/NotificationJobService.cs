using FMC.Application.Interfaces;
using FMC.Domain.Entities;
using FMC.Infrastructure.Authentication;
using FMC.Infrastructure.Data;
using FMC.Shared.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FMC.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire-executed job class responsible for all financial notification emails.
/// Each method is a discrete, independently atomic job that Hangfire can serialize,
/// persist to SQL Server, retry on failure, and monitor via the dashboard.
///
/// Design principles:
/// - Each method receives only primitive types (Guid, string, decimal) — no complex objects.
///   This ensures Hangfire can safely serialize job parameters to SQL.
/// - All methods are invocable by Hangfire in a fresh DI scope — no shared state assumptions.
/// - Failures are isolated: a failed approval email does not affect a capacity alert.
/// </summary>
public sealed class NotificationJobService
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _templateService;
    private readonly ISystemAlertService _alertService;
    private readonly ILogger<NotificationJobService> _logger;

    public NotificationJobService(
        ApplicationDbContext context,
        IEmailService emailService,
        IEmailTemplateService templateService,
        ISystemAlertService alertService,
        ILogger<NotificationJobService> logger)
    {
        _context = context;
        _emailService = emailService;
        _templateService = templateService;
        _alertService = alertService;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────
    // Job 1: Pending Transaction — notify Approvers and CEO
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Sends approval-request emails to all eligible Approvers and the CEO when a
    /// Maker submits a new transaction. Executed asynchronously in the background.
    /// </summary>
    /// <param name="organizationId">The organization scoping this transaction.</param>
    /// <param name="targetUserId">The cardholder receiving the funds.</param>
    /// <param name="makerName">Display name of the Maker who initiated the request.</param>
    /// <param name="amount">Transaction amount for display in the notification.</param>
    public async Task SendPendingApprovalNotificationAsync(
        Guid transactionId,
        Guid organizationId,
        string targetUserId,
        string makerName,
        decimal amount)
    {
        try
        {
            _logger.LogInformation("[BackgroundJob] Sending PendingApproval notification for Org {OrgId}", organizationId);

            var org = await _context.Organizations.IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == organizationId);

            var cardholder = await _context.Cardholders.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id.ToString() == targetUserId);

            if (org == null || cardholder == null)
            {
                _logger.LogWarning("[BackgroundJob] PendingApproval aborted — Org or Cardholder not found.");
                return;
            }

            var approverEmails = await (from u in _context.Users.OfType<ApplicationUser>()
                                        where u.OrganizationId == org.Id
                                        join ur in _context.UserRoles on u.Id equals ur.UserId
                                        join r in _context.Roles on ur.RoleId equals r.Id
                                        where r.Name == FMC.Shared.Auth.Roles.Approver
                                        select u.Email)
                                        .ToListAsync();

            var ceoEmail = await ResolveCeoEmailAsync(org.ChiefExecutiveId);

            var recipients = approverEmails.Where(e => !string.IsNullOrEmpty(e))
                                           .ToHashSet();
            if (!string.IsNullOrEmpty(ceoEmail)) recipients.Add(ceoEmail);

            if (!recipients.Any())
            {
                _logger.LogInformation("[BackgroundJob] No recipients found for PendingApproval — skipping.");
                return;
            }

            var body = _templateService.GeneratePendingApprovalEmail(
                org.Name,
                makerName,
                $"{cardholder.FirstName} {cardholder.LastName}",
                FinanceUtils.MaskCard(cardholder.AccountNumber),
                amount);

            var attachments = BuildAttachments();

            foreach (var email in recipients)
            {
                if (string.IsNullOrEmpty(email)) continue;

                if (await ShouldSendNotificationAsync("PENDING_APPROVAL", transactionId, email))
                {
                    await _emailService.SendEmailAsync(
                        email,
                        $"FMC Action Required: Pending Approval for {org.Name}",
                        body,
                        attachments);
                    
                    await LogNotificationSentAsync("PENDING_APPROVAL", transactionId, email);
                }
            }

            _logger.LogInformation("[BackgroundJob] PendingApproval notification sent to {Count} recipients.", recipients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackgroundJob] Failed to send PendingApproval notification for Org {OrgId}", organizationId);
            throw; // Re-throw so Hangfire registers the failure and retries automatically
        }
    }

    // ─────────────────────────────────────────────────────────
    // Job: Bulk Upload Submitted — notify Approvers and CEO
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a batch-summary email to all eligible Approvers and the CEO when a
    /// Maker submits a bulk transaction file.
    /// </summary>
    public async Task SendBulkUploadNotificationAsync(
        Guid batchId,
        Guid organizationId,
        string makerName,
        int totalCount,
        decimal totalAmount,
        bool isCredit,
        List<FMC.Shared.DTOs.BulkTransactionRowDto>? sampleRows = null)
    {
        try
        {
            _logger.LogInformation("[BackgroundJob] Sending BulkUpload notification for Org {OrgId}", organizationId);

            var org = await _context.Organizations.IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == organizationId);

            if (org == null) return;

            var approverEmails = await (from u in _context.Users.OfType<ApplicationUser>()
                                        where u.OrganizationId == org.Id
                                        join ur in _context.UserRoles on u.Id equals ur.UserId
                                        join r in _context.Roles on ur.RoleId equals r.Id
                                        where r.Name == FMC.Shared.Auth.Roles.Approver
                                        select u.Email)
                                        .ToListAsync();

            var ceoEmail = await ResolveCeoEmailAsync(org.ChiefExecutiveId);

            var recipients = approverEmails.Where(e => !string.IsNullOrEmpty(e)).ToHashSet();
            if (!string.IsNullOrEmpty(ceoEmail)) recipients.Add(ceoEmail);

            if (!recipients.Any()) return;

            var body = _templateService.GenerateBulkUploadNotificationEmail(
                org.Name,
                makerName,
                totalCount,
                totalAmount,
                isCredit,
                sampleRows);

            var attachments = BuildAttachments();

            foreach (var email in recipients)
            {
                if (string.IsNullOrEmpty(email)) continue;

                if (await ShouldSendNotificationAsync("BULK_SUBMITTED", batchId, email))
                {
                    await _emailService.SendEmailAsync(
                        email,
                        $"FMC Action Required: Bulk Batch Settlement — {org.Name}",
                        body,
                        attachments);

                    await LogNotificationSentAsync("BULK_SUBMITTED", batchId, email);
                }
            }

            _logger.LogInformation("[BackgroundJob] BulkUpload notification sent to {Count} recipients.", recipients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackgroundJob] Failed to send BulkUpload notification for Org {OrgId}", organizationId);
            throw;
        }
    }


    // ─────────────────────────────────────────────────────────
    // Job 2: Transaction Approved — notify Maker and CEO
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Sends settlement confirmation emails to the Maker and CEO after a transaction
    /// is approved and funds are transferred. Executes capacity threshold checks as a
    /// chained responsibility within the same job scope.
    /// </summary>
    /// <param name="transactionId">The approved transaction's unique identifier.</param>
    /// <param name="organizationId">The organization scoping this transaction.</param>
    public async Task SendApprovalConfirmationAsync(Guid transactionId, Guid organizationId)
    {
        try
        {
            _logger.LogInformation("[BackgroundJob] Sending ApprovalConfirmation for Transaction {TxId}", transactionId);

            var transaction = await _context.Transactions.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == transactionId);

            var org = await _context.Organizations.IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == organizationId);

            if (transaction == null || org == null)
            {
                _logger.LogWarning("[BackgroundJob] ApprovalConfirmation aborted — Transaction or Org not found.");
                return;
            }

            var cardholder = await _context.Cardholders.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id.ToString() == transaction.TenantId);

            var makerEmail = await _context.Users.IgnoreQueryFilters()
                .Where(u => u.Id == transaction.MakerId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            var ceoEmail = await ResolveCeoEmailAsync(org.ChiefExecutiveId);

            var recipients = new HashSet<string>();
            if (!string.IsNullOrEmpty(makerEmail)) recipients.Add(makerEmail);
            if (!string.IsNullOrEmpty(ceoEmail)) recipients.Add(ceoEmail);

            if (recipients.Any())
            {
                var targetName = cardholder != null ? $"{cardholder.FirstName} {cardholder.LastName}" : "Subscriber";
                var targetCard = FinanceUtils.MaskCard(cardholder?.AccountNumber ?? "N/A");

                var body = _templateService.GenerateTransactionApprovedEmail(org.Name, targetName, targetCard, transaction.Amount);
                var attachments = BuildAttachments();

                foreach (var email in recipients)
                {
                    if (await ShouldSendNotificationAsync("TRANSACTION_APPROVED", transactionId, email))
                    {
                        await _emailService.SendEmailAsync(
                            email,
                            $"FMC Notification: Transaction Approved — {org.Name}",
                            body,
                            attachments);
                            
                        await LogNotificationSentAsync("TRANSACTION_APPROVED", transactionId, email);
                    }
                }

                _logger.LogInformation("[BackgroundJob] ApprovalConfirmation sent to {Count} recipients.", recipients.Count);
            }

            // Chained responsibility: check capacity AFTER approval emails are sent
            await SendCapacityAlertIfNeededAsync(org, ceoEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackgroundJob] Failed to send ApprovalConfirmation for Transaction {TxId}", transactionId);
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────
    // Job: Batch Approved — notify Maker and CEO with summary
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a consolidated settlement confirmation email to the Maker and CEO after a 
    /// batch changes status (Approved, Rejected, Cancelled). This prevents email flooding.
    /// </summary>
    public async Task SendBatchStatusNotificationAsync(Guid batchId, Guid organizationId, string status, string? reason = null)
    {
        try
        {
            _logger.LogInformation("[BackgroundJob] Sending BatchStatusNotification ({Status}) for Batch {BatchId}", status, batchId);

            var transactionsWithCardholders = await (from t in _context.Transactions.IgnoreQueryFilters()
                                                     where t.BatchId == batchId
                                                     join c in _context.Cardholders.IgnoreQueryFilters() on t.TenantId equals c.Id.ToString() into cardholders
                                                     from ch in cardholders.DefaultIfEmpty()
                                                     select new {
                                                         Transaction = t,
                                                         Cardholder = ch
                                                     }).ToListAsync();

            var org = await _context.Organizations.IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == organizationId);

            if (!transactionsWithCardholders.Any() || org == null)
            {
                _logger.LogWarning("[BackgroundJob] BatchStatusNotification aborted — Batch or Org not found.");
                return;
            }

            var transactionDtos = transactionsWithCardholders.Select(x => new FMC.Shared.DTOs.TransactionDto {
                Id = x.Transaction.Id,
                Amount = x.Transaction.Amount,
                Status = x.Transaction.Status,
                Date = x.Transaction.Date,
                Subscriber = x.Cardholder != null ? $"{x.Cardholder.FirstName} {x.Cardholder.LastName}" : "Subscriber",
                AccountNumber = x.Cardholder?.AccountNumber ?? "N/A"
            }).ToList();

            var ceoEmail = await ResolveCeoEmailAsync(org.ChiefExecutiveId);
            var makerId = transactionsWithCardholders.First().Transaction.MakerId;
            var makerEmail = await _context.Users.IgnoreQueryFilters()
                .Where(u => u.Id == makerId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            var recipients = new HashSet<string>();
            if (!string.IsNullOrEmpty(makerEmail)) recipients.Add(makerEmail);
            if (!string.IsNullOrEmpty(ceoEmail)) recipients.Add(ceoEmail);

            if (recipients.Any())
            {
                var body = _templateService.GenerateBatchNotificationEmail(
                    org.Name, 
                    status, 
                    transactionDtos, 
                    transactionsWithCardholders.Count > 20);
                    
                var attachments = BuildAttachments();
                var subject = $"FMC Notification: Batch {status} — {org.Name}";

                foreach (var email in recipients)
                {
                    if (await ShouldSendNotificationAsync($"BATCH_{status.ToUpper()}", batchId, email))
                    {
                        await _emailService.SendEmailAsync(email, subject, body, attachments);
                        await LogNotificationSentAsync($"BATCH_{status.ToUpper()}", batchId, email);
                    }
                }

                _logger.LogInformation("[BackgroundJob] BatchStatusNotification ({Status}) sent to {Count} recipients.", status, recipients.Count);
            }

            // Chained responsibility: only check capacity for approvals
            if (status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
            {
                await SendCapacityAlertIfNeededAsync(org, ceoEmail);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackgroundJob] Failed to send BatchStatusNotification for Batch {BatchId}", batchId);
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────
    // Job 3: Wallet Adjusted — notify CEO of org-level credits/debits
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a wallet adjustment advisory email to the CEO after a SuperAdmin
    /// credits or debits the organization's core operations wallet.
    /// </summary>
    public async Task SendWalletAdjustmentNotificationAsync(
        Guid adjustmentId,
        Guid organizationId,
        decimal amount,
        decimal newBalance)
    {
        try
        {
            _logger.LogInformation("[BackgroundJob] Sending WalletAdjustment notification for Org {OrgId}", organizationId);

            var org = await _context.Organizations.IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == organizationId);

            if (org == null) return;

            var ceoEmail = await ResolveCeoEmailAsync(org.ChiefExecutiveId);
            if (string.IsNullOrEmpty(ceoEmail))
            {
                _logger.LogInformation("[BackgroundJob] WalletAdjustment skipped — no CEO email configured for Org {OrgId}", organizationId);
                return;
            }

            var isCredit = amount > 0;
            var body = _templateService.GenerateWalletAdjustmentEmail(
                org.Name,
                FinanceUtils.MaskCard(org.AccountNumber),
                amount,
                newBalance,
                isCredit);

            if (await ShouldSendNotificationAsync("WALLET_ADJUSTMENT", adjustmentId, ceoEmail))
            {
                var actionTitle = isCredit ? "Wallet Credited Successfully" : "Wallet Adjustment (Debit)";
                await _emailService.SendEmailAsync(
                    ceoEmail,
                    $"FMC Advisory: {actionTitle} — {org.Name}",
                    body,
                    BuildAttachments());
                    
                await LogNotificationSentAsync("WALLET_ADJUSTMENT", adjustmentId, ceoEmail);
            }

            _logger.LogInformation("[BackgroundJob] WalletAdjustment notification sent to CEO for Org {OrgId}", organizationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackgroundJob] Failed to send WalletAdjustment notification for Org {OrgId}", organizationId);
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────
    // Job 4: Data Retention — cleanup old resolved alerts
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Periodic cleanup job that removes resolved system alerts older than the
    /// specified retention period (default 90 days).
    /// </summary>
    public async Task CleanupOldSystemAlertsJobAsync()
    {
        try
        {
            const int RetentionDays = 90;
            _logger.LogInformation("[BackgroundJob] Starting SystemAlerts cleanup (Retention: {Days} days)", RetentionDays);
            
            await _alertService.CleanupOldAlertsAsync(RetentionDays);
            
            _logger.LogInformation("[BackgroundJob] SystemAlerts cleanup completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackgroundJob] Failed to execute SystemAlerts cleanup job.");
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────
    // Private Helpers
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Checks if the organization's wallet utilization has exceeded thresholds and,
    /// if so, dispatches a capacity advisory email to the CEO.
    /// </summary>
    private async Task SendCapacityAlertIfNeededAsync(Organization org, string? ceoEmail)
    {
        if (string.IsNullOrEmpty(ceoEmail)) return;

        var orgTenantId = org.Id.ToString();

        var orgBalance = await _context.Accounts.IgnoreQueryFilters()
            .Where(a => a.TenantId == orgTenantId)
            .Select(a => a.Balance)
            .FirstOrDefaultAsync();

        var userBalanceSum = await _context.Accounts.IgnoreQueryFilters()
            .Where(a => a.OrganizationId == org.Id && a.TenantId != orgTenantId)
            .SumAsync(a => a.Balance);

        var total = orgBalance + userBalanceSum;
        var usedPct = total > 0 ? (userBalanceSum / total) * 100m : 0m;

        if (usedPct >= 80m || orgBalance <= 100_000m)
        {
            var alertType = usedPct >= 80m
                ? $"{usedPct:F0}% Operational Capacity Alert"
                : "Critical Liquidity Advisory";

            var body = _templateService.GenerateCapacityThresholdEmail(
                org.Name, total, userBalanceSum, usedPct, orgBalance);

            await _emailService.SendEmailAsync(
                ceoEmail,
                $"FMC Advisory: {alertType} — {org.Name}",
                body,
                BuildAttachments());

            _logger.LogWarning("[BackgroundJob] Capacity alert dispatched for Org {OrgId}. Used: {Pct:F1}%", org.Id, usedPct);
        }
    }

    /// <summary>Resolves a CEO's email address by their Identity user ID.</summary>
    private async Task<string?> ResolveCeoEmailAsync(string? ceoId)
    {
        if (string.IsNullOrEmpty(ceoId)) return null;
        return await _context.Users.IgnoreQueryFilters()
            .Where(u => u.Id == ceoId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Builds the standard email attachment dictionary containing the company brand logo.
    /// Centralised here to ensure consistent branding across all notification types.
    /// </summary>
    private static Dictionary<string, byte[]> BuildAttachments() => new()
    {
        { "nlklogo", Convert.FromBase64String(BrandingConstants.NationlinkLogoBase64) }
    };

    // ─────────────────────────────────────────────────────────
    // Idempotency Engine
    // ─────────────────────────────────────────────────────────

    private async Task<bool> ShouldSendNotificationAsync(string action, Guid entityId, string recipient)
    {
        var key = $"{action}:{entityId}:{recipient.ToLower().Trim()}";
        var alreadySent = await _context.NotificationAudits
            .AnyAsync(a => a.NotificationKey == key);
            
        if (alreadySent)
        {
            _logger.LogInformation("[Idempotency] Skipping duplicate notification: {Key}", key);
            return false;
        }
        
        return true;
    }

    private async Task LogNotificationSentAsync(string action, Guid entityId, string recipient, string? providerId = null)
    {
        var key = $"{action}:{entityId}:{recipient.ToLower().Trim()}";
        var audit = new NotificationAudit
        {
            NotificationKey = key,
            ActionType = action,
            EntityId = entityId.ToString(),
            Recipient = recipient,
            ProviderMessageId = providerId,
            SentAt = DateTime.UtcNow,
            Status = "SENT"
        };
        
        _context.NotificationAudits.Add(audit);
        await _context.SaveChangesAsync();
    }
}
