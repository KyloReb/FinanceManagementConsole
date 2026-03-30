using System.Collections.Generic;
using System.Threading.Tasks;

namespace FMC.Application.Interfaces;

public interface IAuditService
{
    Task RecordAuthEventAsync(string action, string? userId, string ipAddress, string device, string details);
    Task<List<FMC.Shared.DTOs.Admin.AuditLogDto>> GetAuthLogsAsync();
}
