using FMC.Shared.DTOs.Admin;
using System.Threading;
using System.Threading.Tasks;

namespace FMC.Application.Interfaces;

/// <summary>
/// Service responsible for aggregating system-wide health metrics, diagnostic checks,
/// and background job statistics into a unified monitoring snapshot.
/// </summary>
public interface ISystemHealthService
{
    /// <summary>
    /// Performs a real-time diagnostic sweep of the infrastructure and returns 
    /// a high-level health report for the SuperAdmin console.
    /// </summary>
    /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
    /// <returns>A DTO containing infrastructure, performance, and job pipeline metrics.</returns>
    Task<SystemHealthDto> GetSystemHealthPulseAsync(CancellationToken cancellationToken = default);
}
