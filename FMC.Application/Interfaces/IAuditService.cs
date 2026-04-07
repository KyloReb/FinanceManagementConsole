using System.Collections.Generic;
using System.Threading.Tasks;

namespace FMC.Application.Interfaces;

public interface IAuditService
{
    Task RecordAuthEventAsync(string action, string? userId, string ipAddress, string device, string details);
    
    Task RecordFinancialEventAsync(string action, Guid entityId, string entityName, decimal amount, string label, string performedBy, string? details = null, string? tenantId = null);
    
    Task<List<FMC.Shared.DTOs.Admin.AuditLogDto>> GetAuthLogsAsync();
    
    Task<List<FMC.Shared.DTOs.Admin.AuditLogDto>> GetRecentLogsAsync(int count = 20, string? category = null, string? tenantId = null);
    
    Task<FMC.Shared.DTOs.Admin.AuditLogSearchResultDto> SearchLogsAsync(FMC.Shared.DTOs.Admin.AuditLogQueryDto query);
}
