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

            // Identify the data range.
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            
            // Security Constraint: Enforce 100-row maximum processing limit to prevent resource exhaustion.
            // We read up to row 101 (Index 1 is Header, 2-101 are Data).
            int processingLimit = Math.Min(lastRow, 101);

            for (int r = 2; r <= processingLimit; r++)
            {
                // Extract values using GetValue helper which handles type conversion.
                var subscriber = worksheet.Cell(r, 1).GetValue<string>()?.Trim() ?? string.Empty;
                var cardNumber = worksheet.Cell(r, 2).GetValue<string>()?.Trim() ?? string.Empty;
                
                // Skip completely empty rows.
                if (string.IsNullOrWhiteSpace(subscriber) && string.IsNullOrWhiteSpace(cardNumber))
                {
                    continue;
                }

                decimal amount = 0;
                string? validationError = null;

                // 1. Basic Formatting & Amount Validation
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

                // 2. Identity Verification (Name vs Card Number)
                if (validationError == null && !string.IsNullOrWhiteSpace(cardNumber))
                {
                    var cardholder = await _repository.GetCardholderByAccountNumberAsync(cardNumber ?? "", organizationId, ct);
                    if (cardholder == null)
                    {
                        // Check if it exists at all to give a better error message
                        validationError = "Card number not found in our records.";
                    }
                    else
                    {
                        var fullName = $"{cardholder.FirstName} {cardholder.LastName}".Trim();
                        if (!string.Equals(subscriber, fullName, StringComparison.OrdinalIgnoreCase))
                        {
                            validationError = $"Identity Mismatch: Card belongs to '{fullName}', not '{subscriber}'.";
                        }
                    }
                }

                rows.Add(new BulkTransactionRowDto
                {
                    RowNumber = r - 1, // 1-based data row index for UI display
                    Subscriber = subscriber,
                    CardNumber = cardNumber,
                    Amount = amount,
                    ValidationError = validationError
                });
            }
        }
        catch (Exception ex)
        {
            // Propagate meaningful error messages to the UI layer.
            throw new InvalidOperationException($"The Excel file could not be parsed: {ex.Message}", ex);
        }

        return rows;
    }

    /// <summary>
    /// Re-validates a list of existing rows against the current database state.
    /// This is used when a user manually edits a row in the UI.
    /// </summary>
    public async Task<List<BulkTransactionRowDto>> ValidateRowsAsync(List<BulkTransactionRowDto> rows, Guid organizationId, CancellationToken ct = default)
    {
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

            var cleanCardNumber = row.CardNumber?.Trim();
            var cardholder = await _repository.GetCardholderByAccountNumberAsync(cleanCardNumber ?? "", organizationId, ct);
            if (cardholder == null)
            {
                row.ValidationError = "Card number not found in our records.";
            }
            else
            {
                var fullName = $"{cardholder.FirstName} {cardholder.LastName}".Trim();
                if (!string.Equals(row.Subscriber?.Trim(), fullName, StringComparison.OrdinalIgnoreCase))
                {
                    row.ValidationError = $"Identity Mismatch: Card belongs to '{fullName}', not '{row.Subscriber}'.";
                }
            }
        }
        return rows;
    }
}
