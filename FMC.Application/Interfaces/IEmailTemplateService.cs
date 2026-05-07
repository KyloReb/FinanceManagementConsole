namespace FMC.Application.Interfaces;

/// <summary>
/// Service responsible for generating premium HTML email templates for financial workflows.
/// Decouples UI/UX presentation from core business logic.
/// </summary>
public interface IEmailTemplateService
{
    /// <summary>
    /// Generates the HTML body for a pending transaction validation request.
    /// </summary>
    string GeneratePendingApprovalEmail(string orgName, string makerName, string targetCardholder, string maskedCardNumber, decimal amount);

    /// <summary>
    /// Generates the HTML body for a successful transaction approval notification.
    /// </summary>
    string GenerateTransactionApprovedEmail(string orgName, string targetCardholder, string maskedCardNumber, decimal amount);

    /// <summary>
    /// Generates the HTML body for organizational wallet adjustments (Credits/Debits).
    /// </summary>
    string GenerateWalletAdjustmentEmail(string orgName, string maskedCardNumber, decimal amount, decimal balance, bool isCredit);

    /// <summary>
    /// Generates the HTML advisory for organizations reaching capacity or low liquidity thresholds.
    /// </summary>
    string GenerateCapacityThresholdEmail(string orgName, decimal total, decimal dispersed, decimal pct, decimal remaining);

    /// <summary>
    /// Generates the HTML body for a bulk upload submission notification.
    /// Includes an optional sample of transactions for preview.
    /// </summary>
    string GenerateBulkUploadNotificationEmail(string orgName, string makerName, int totalCount, decimal totalAmount, bool isCredit, List<FMC.Shared.DTOs.BulkTransactionRowDto>? sampleRows = null);

    /// <summary>
    /// Generates the HTML body for a unified batch approval/rejection notification.
    /// </summary>
    string GenerateBatchNotificationEmail(string orgName, string batchAction, IEnumerable<FMC.Shared.DTOs.TransactionDto> transactions, bool hasAttachments);
}
