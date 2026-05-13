using FMC.Application.Interfaces;
using FMC.Application.Organizations.Events;
using FMC.Domain.Entities;
using FMC.Shared.DTOs;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FMC.Application.Transactions.Commands;

public class SubmitBulkTransactionCommandHandler : IRequestHandler<SubmitBulkTransactionCommand, BulkUploadResultDto>
{
    private readonly IOrganizationRepository _repository;
    private readonly IPublisher _publisher;
    private readonly IAuditService _auditService;
    private readonly ILogger<SubmitBulkTransactionCommandHandler> _logger;

    public SubmitBulkTransactionCommandHandler(
        IOrganizationRepository repository, 
        IPublisher publisher,
        IAuditService auditService,
        ILogger<SubmitBulkTransactionCommandHandler> logger)
    {
        _repository = repository;
        _publisher = publisher;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<BulkUploadResultDto> Handle(SubmitBulkTransactionCommand request, CancellationToken cancellationToken)
    {
        int submitted = 0;
        int failed = 0;
        
        var resultRows = new List<BulkTransactionRowDto>();

        var batchId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        // 0. Idempotency Check: Prevent duplicate batch processing
        if (!string.IsNullOrEmpty(request.BatchIdempotencyKey))
        {
            if (await _repository.ExistsTransactionWithIdempotencyKeyAsync(request.BatchIdempotencyKey, cancellationToken))
            {
                _logger.LogWarning("Bulk Transaction: Duplicate Batch detected. Key: {Key}", request.BatchIdempotencyKey);
                return new BulkUploadResultDto { TotalRows = request.Rows.Count, Submitted = 0, Failed = 0, Rows = new() };
            }
        }

        foreach (var row in request.Rows)
        {
            try
            {
                // 1. Resolve Account by Card Number within the Organization context
                var cleanCardNumber = row.CardNumber?.Trim();
                var account = await _repository.GetAccountByCardNumberAsync(cleanCardNumber ?? "", request.OrganizationId, cancellationToken);
                
                if (account == null)
                {
                    _logger.LogWarning("Bulk Transaction: Card holder {CardNumber} not found in Organization {OrgId}", cleanCardNumber, request.OrganizationId);
                    row.ValidationError = "Card number not found in our records.";
                    failed++;
                    resultRows.Add(row);
                    continue;
                }

                // 2. Create PENDING Transaction
                var transaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    BatchId = batchId,
                    Label = $"Bulk {(request.IsCredit ? "Credit" : "Debit")} Allotment",
                    Amount = request.IsCredit ? row.Amount : -row.Amount,
                    Date = timestamp,
                    Category = "Bulk Allotment",
                    TenantId = account.TenantId,
                    AccountId = account.Id,
                    Status = "Pending",
                    MakerId = request.MakerId,
                    OrganizationId = request.OrganizationId,
                    IdempotencyKey = string.IsNullOrEmpty(request.BatchIdempotencyKey) ? null : $"{request.BatchIdempotencyKey}:{row.RowNumber}"
                };

                // Tag the first row with the actual BatchIdempotencyKey to block future duplicate batches
                if (submitted == 0 && !string.IsNullOrEmpty(request.BatchIdempotencyKey))
                {
                    transaction.IdempotencyKey = request.BatchIdempotencyKey;
                }

                await _repository.AddTransactionAsync(transaction, cancellationToken);
                
                submitted++;
                row.ResolvedUserId = Guid.TryParse(account.TenantId, out var uid) ? uid : null;
                resultRows.Add(row);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bulk row {RowNumber} for Card {CardNumber}", row.RowNumber, row.CardNumber);
                row.ValidationError = "Internal processing error.";
                failed++;
                resultRows.Add(row);
            }
        }

        // 4. Atomic Commitment for all valid rows (Partial Success implementation)
        if (submitted > 0)
        {
            await _repository.SaveChangesAsync(cancellationToken);
            
            // 5. Notify Approvers about the new Batch (Include up to 20 samples)
            var totalAmount = resultRows.Where(r => string.IsNullOrEmpty(r.ValidationError)).Sum(r => r.Amount);
            var sampleRows = resultRows.Where(r => string.IsNullOrEmpty(r.ValidationError)).Take(20).ToList();

            await _publisher.Publish(new BulkUploadSubmittedEvent(
                batchId,
                request.OrganizationId,
                request.MakerName,
                submitted,
                totalAmount,
                request.IsCredit,
                sampleRows), cancellationToken);
                
            // 6. Record consolidated audit trail for batch initiation with structured JSON breakdown
            var batchDetails = new {
                OrganizationId = request.OrganizationId,
                MakerName = request.MakerName,
                TotalAmount = totalAmount,
                ItemCount = submitted,
                IsCredit = request.IsCredit,
                Items = sampleRows.Select(r => new { r.CardNumber, r.Amount, r.Subscriber })
            };
            var jsonBreakdown = System.Text.Json.JsonSerializer.Serialize(batchDetails);

            await _auditService.RecordFinancialEventAsync(
                "BATCH_SUBMITTED", 
                request.OrganizationId, 
                $"{submitted} Cardholders (Bulk)", 
                totalAmount, 
                $"Bulk {(request.IsCredit ? "Credit" : "Debit")} Allotment Initiation", 
                request.MakerName, // Operator Name
                jsonBreakdown,     // Structured JSON Payload
                request.OrganizationId.ToString());
        }

        return new BulkUploadResultDto
        {
            TotalRows = request.Rows.Count,
            Submitted = submitted,
            Failed = failed,
            Rows = resultRows
        };
    }
}
