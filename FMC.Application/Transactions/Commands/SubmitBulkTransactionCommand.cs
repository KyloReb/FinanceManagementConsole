using FMC.Shared.DTOs;
using MediatR;

namespace FMC.Application.Transactions.Commands;

public record SubmitBulkTransactionCommand(
    Guid OrganizationId,
    string MakerId,
    string MakerName,
    bool IsCredit,
    List<BulkTransactionRowDto> Rows
) : IRequest<BulkUploadResultDto>;
