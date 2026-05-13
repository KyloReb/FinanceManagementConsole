using FMC.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading;
using System.Threading.Tasks;

namespace FMC.Infrastructure.Diagnostics;

/// <summary>
/// Custom health check for the financial database using EF Core's connectivity check.
/// This avoids external dependencies on specific health check packages.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _context;

    public DatabaseHealthCheck(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            return canConnect 
                ? HealthCheckResult.Healthy("Financial Database is reachable.") 
                : HealthCheckResult.Unhealthy("Financial Database is unreachable.");
        }
        catch (System.Exception ex)
        {
            return HealthCheckResult.Unhealthy("Financial Database check failed.", ex);
        }
    }
}
