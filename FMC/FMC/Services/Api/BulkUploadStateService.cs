using FMC.Shared.DTOs;

namespace FMC.Services.Api;

/// <summary>
/// Scoped service to maintain state during navigation between the Bulk Upload Dialog and the Full-Page Manager.
/// </summary>
public class BulkUploadStateService
{
    public List<BulkTransactionRowDto> PendingRows { get; set; } = new();
    public bool IsCredit { get; set; } = true;
    public Guid OrgId { get; set; }
    public string? OrganizationName { get; set; }
}
