using FMC.Shared.DTOs;

namespace FMC.Application.Interfaces;

public interface IExcelParserService
{
    /// <summary>
    /// Parses a .xlsx stream into a list of cardholder rows.
    /// Expected columns: Subscriber, CardNumber, Amount
    /// </summary>
    Task<List<BulkTransactionRowDto>> ParseCardholdersAsync(Stream fileStream, Guid organizationId, CancellationToken ct = default);
    Task<List<BulkTransactionRowDto>> ValidateRowsAsync(List<BulkTransactionRowDto> rows, Guid organizationId, CancellationToken ct = default);
}
