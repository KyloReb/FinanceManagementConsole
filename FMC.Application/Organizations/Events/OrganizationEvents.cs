using FMC.Shared.DTOs;
using MediatR;
using System.Collections.Generic;

namespace FMC.Application.Organizations.Events;

/// <summary>
/// Event raised when a Maker initiates a new transaction that requires approval.
/// </summary>
public record TransactionPendingEvent(
    Guid TransactionId,
    Guid OrganizationId, 
    string MakerName, 
    string TargetUserId, 
    decimal Amount, 
    string Label) : INotification;

/// <summary>
/// Event raised when an Approver successfully commits a pending transaction.
/// </summary>
public record TransactionApprovedEvent(
    Guid OrganizationId, 
    Guid TransactionId) : INotification;

/// <summary>
/// Event raised when the organizational wallet is adjusted by a SuperAdmin.
/// </summary>
public record WalletAdjustedEvent(
    Guid AdjustmentId,
    Guid OrganizationId, 
    decimal Amount, 
    decimal NewBalance, 
    string Label) : INotification;

/// <summary>
/// Event raised when a Maker submits a batch of transactions via bulk upload.
/// </summary>
public record BulkUploadSubmittedEvent(
    Guid BatchId,
    Guid OrganizationId,
    string MakerName,
    int TotalCount,
    decimal TotalAmount,
    bool IsCredit,
    List<BulkTransactionRowDto> SampleRows) : INotification;

/// <summary>
/// Event raised when an Approver commits an entire batch of transactions.
/// </summary>
public record BatchApprovedEvent(
    Guid OrganizationId,
    Guid BatchId,
    string ApproverId) : INotification;

/// <summary>
/// Event raised when an Approver rejects an entire batch of transactions.
/// </summary>
public record BatchRejectedEvent(
    Guid OrganizationId,
    Guid BatchId,
    string ApproverId,
    string Reason) : INotification;

/// <summary>
/// Event raised when a Maker cancels their own pending batch.
/// </summary>
public record BatchCancelledEvent(
    Guid OrganizationId,
    Guid BatchId,
    string MakerId) : INotification;
