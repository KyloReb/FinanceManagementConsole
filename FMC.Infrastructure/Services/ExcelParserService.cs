using FMC.Application.Interfaces;
using FMC.Shared.DTOs;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FMC.Infrastructure.Services;

/// <summary>
/// Service responsible for parsing financial batch data from Excel workbooks.
/// Uses ClosedXML for robust XLSX processing.
/// </summary>
public class ExcelParserService : IExcelParserService
{
    private readonly IOrganizationRepository _repository;

    public ExcelParserService(IOrganizationRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Parses a cardholder transaction list from an uploaded Excel file.
    /// Expected format: Column 1: Subscriber Name, Column 2: Card Number, Column 3: Amount.
    /// </summary>
    /// <param name="fileStream">The stream containing the Excel file data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of DTOs representing the rows and any validation errors found during parsing.</returns>
    public async Task<List<BulkTransactionRowDto>> ParseCardholdersAsync(Stream fileStream, Guid organizationId, CancellationToken ct = default)
    {
        var rows = new List<BulkTransactionRowDto>();

        try
        {
            // Open the workbook from the stream.
            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            
            if (worksheet == null) 
            {
                return rows;
            }

            // High-speed In-Memory Dictionary Lookup to avoid N+1 DB Queries
            var allCardholders = await _repository.GetCardholdersByOrganizationAsync(organizationId, ct);
            var cardholderDict = allCardholders
                .Where(c => !string.IsNullOrEmpty(c.AccountNumber))
                .GroupBy(c => c.AccountNumber!)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            
            // Limit safely increased to 10,000 data rows thanks to O(1) in-memory lookups
            int processingLimit = Math.Min(lastRow, 10001);

            for (int r = 2; r <= processingLimit; r++)
            {
                var subscriber = worksheet.Cell(r, 1).GetValue<string>()?.Trim() ?? string.Empty;
                var cardNumber = worksheet.Cell(r, 2).GetValue<string>()?.Trim() ?? string.Empty;
                
                if (string.IsNullOrWhiteSpace(subscriber) && string.IsNullOrWhiteSpace(cardNumber))
                {
                    continue;
                }

                decimal amount = 0;
                string? validationError = null;

                try 
                {
                    amount = worksheet.Cell(r, 3).GetValue<decimal>();
                    if (amount <= 0) 
                    {
                        validationError = "Transaction amount must be a positive value.";
                    }
                }
                catch (Exception)
                {
                    validationError = "Invalid amount format. Please ensure the cell contains a numeric value.";
                }

                if (validationError == null && !string.IsNullOrWhiteSpace(cardNumber))
                {
                    if (!cardholderDict.TryGetValue(cardNumber, out var cardholder))
                    {
                        validationError = "Card number not found in our records.";
                    }
                    else
                    {
                        // Auto-correct subscriber name to match the system's exact record
                        // if they entered it incorrectly, rather than failing the validation.
                        subscriber = $"{cardholder.FirstName} {cardholder.LastName}".Trim();
                    }
                }

                rows.Add(new BulkTransactionRowDto
                {
                    RowNumber = r - 1, 
                    Subscriber = subscriber ?? string.Empty,
                    CardNumber = cardNumber ?? string.Empty,
                    Amount = amount,
                    ValidationError = validationError
                });
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"The Excel file could not be parsed: {ex.Message}", ex);
        }

        return rows;
    }

    /// <summary>
    /// Re-validates a list of existing rows against the current database state using bulk memory load.
    /// </summary>
    public async Task<List<BulkTransactionRowDto>> ValidateRowsAsync(List<BulkTransactionRowDto> rows, Guid organizationId, CancellationToken ct = default)
    {
        var allCardholders = await _repository.GetCardholdersByOrganizationAsync(organizationId, ct);
        var cardholderDict = allCardholders
            .Where(c => !string.IsNullOrEmpty(c.AccountNumber))
            .GroupBy(c => c.AccountNumber!)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            row.ValidationError = null; 

            if (row.Amount <= 0)
            {
                row.ValidationError = "Transaction amount must be a positive value.";
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.CardNumber))
            {
                row.ValidationError = "Card number is required.";
                continue;
            }

            var cleanCardNumber = row.CardNumber?.Trim() ?? string.Empty;
            
            if (!cardholderDict.TryGetValue(cleanCardNumber, out var cardholder))
            {
                row.ValidationError = "Card number not found in our records.";
            }
            else
            {
                // Align subscriber name with DB record on re-validation
                row.Subscriber = $"{cardholder.FirstName} {cardholder.LastName}".Trim();
            }
        }
        return rows;
    }
}
