using FMC.Application.Interfaces;
using FMC.Application.Organizations.Events;
using FMC.Infrastructure.BackgroundJobs;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FMC.Infrastructure.Services;

/// <summary>
/// MediatR notification handler responsible for dispatching financial workflow events
/// to the background job queue. This handler is the bridge between the synchronous
/// transaction (which the user is waiting on) and the asynchronous notification system.
///
/// Architecture Pattern — "Fire and Forget via Queue":
/// 1. A transaction is committed to the database.
/// 2. MediatR publishes a domain event (e.g., TransactionPendingEvent).
/// 3. This handler picks it up and enqueues a Hangfire background job.
/// 4. The user's request returns immediately — they do NOT wait for emails to send.
/// 5. Hangfire processes the job asynchronously, retrying up to 10 times on failure.
///
/// This pattern ensures:
/// - User experience: fast, non-blocking API responses.
/// - Reliability: emails are never "lost" even if the SMTP server is temporarily down.
/// - Observability: all job executions are visible in the Hangfire Dashboard.
/// </summary>
public sealed class OrganizationNotificationHandler :
    INotificationHandler<TransactionPendingEvent>,
    INotificationHandler<TransactionApprovedEvent>,
    INotificationHandler<BatchApprovedEvent>,
    INotificationHandler<BatchRejectedEvent>,
    INotificationHandler<BatchCancelledEvent>,
    INotificationHandler<BulkUploadSubmittedEvent>,
    INotificationHandler<WalletAdjustedEvent>
{
    private readonly IBackgroundJobService _jobService;
    private readonly ILogger<OrganizationNotificationHandler> _logger;

    public OrganizationNotificationHandler(
        IBackgroundJobService jobService,
        ILogger<OrganizationNotificationHandler> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    /// <summary>
    /// Handles a pending transaction event by enqueuing an approval-request email
    /// job to all eligible Approvers and the CEO.
    /// </summary>
    public Task Handle(TransactionPendingEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[NotificationHandler] Queuing PendingApproval job for Org {OrgId}, Maker: {Maker}, Amount: {Amount}",
            notification.OrganizationId, notification.MakerName, notification.Amount);

        _jobService.Enqueue<NotificationJobService>(job =>
            job.SendPendingApprovalNotificationAsync(
                notification.TransactionId,
                notification.OrganizationId,
                notification.TargetUserId,
                notification.MakerName,
                notification.Amount));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles a bulk upload submission event by enqueuing a batch-summary email job.
    /// </summary>
    public Task Handle(BulkUploadSubmittedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[NotificationHandler] Queuing BulkUpload job for Org {OrgId}, Maker: {Maker}, Count: {Count}",
            notification.OrganizationId, notification.MakerName, notification.TotalCount);

        _jobService.Enqueue<NotificationJobService>(job =>
            job.SendBulkUploadNotificationAsync(
                notification.BatchId,
                notification.OrganizationId,
                notification.MakerName,
                notification.TotalCount,
                notification.TotalAmount,
                notification.IsCredit,
                notification.SampleRows));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles a transaction approval event by enqueuing settlement confirmation
    /// emails to the Maker and CEO.
    /// </summary>
    public Task Handle(TransactionApprovedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[NotificationHandler] Queuing ApprovalConfirmation job for Transaction {TxId}",
            notification.TransactionId);

        _jobService.Enqueue<NotificationJobService>(job =>
            job.SendApprovalConfirmationAsync(
                notification.TransactionId,
                notification.OrganizationId));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles a batch approval event by enqueuing a consolidated settlement confirmation.
    /// </summary>
    public Task Handle(BatchApprovedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[NotificationHandler] Queuing BatchApprovalConfirmation job for Batch {BatchId}",
            notification.BatchId);

        _jobService.Enqueue<NotificationJobService>(job =>
            job.SendBatchStatusNotificationAsync(
                notification.BatchId,
                notification.OrganizationId,
                "Approved",
                null));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles a batch rejection event by enqueuing a consolidated notification.
    /// </summary>
    public Task Handle(BatchRejectedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[NotificationHandler] Queuing BatchRejectionConfirmation job for Batch {BatchId}",
            notification.BatchId);

        _jobService.Enqueue<NotificationJobService>(job =>
            job.SendBatchStatusNotificationAsync(
                notification.BatchId,
                notification.OrganizationId,
                "Rejected",
                notification.Reason));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles a batch cancellation event by enqueuing a consolidated notification.
    /// </summary>
    public Task Handle(BatchCancelledEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[NotificationHandler] Queuing BatchCancellationConfirmation job for Batch {BatchId}",
            notification.BatchId);

        _jobService.Enqueue<NotificationJobService>(job =>
            job.SendBatchStatusNotificationAsync(
                notification.BatchId,
                notification.OrganizationId,
                "Cancelled",
                null));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles a wallet adjustment event by enqueuing an advisory email to the CEO.
    /// </summary>
    public Task Handle(WalletAdjustedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[NotificationHandler] Queuing WalletAdjustment job for Org {OrgId}, Amount: {Amount}",
            notification.OrganizationId, notification.Amount);

        _jobService.Enqueue<NotificationJobService>(job =>
            job.SendWalletAdjustmentNotificationAsync(
                notification.AdjustmentId,
                notification.OrganizationId,
                notification.Amount,
                notification.NewBalance));

        return Task.CompletedTask;
    }
}
