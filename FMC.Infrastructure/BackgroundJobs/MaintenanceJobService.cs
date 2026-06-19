using FMC.Application.Interfaces;
using FMC.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FMC.Infrastructure.BackgroundJobs;

public class MaintenanceJobService
{
    private readonly ICacheService _cacheService;
    private readonly ISystemHealthService _healthService;
    private readonly ISystemAlertService _alertService;
    private readonly ILogger<MaintenanceJobService> _logger;

    public MaintenanceJobService(ICacheService cacheService, ISystemHealthService healthService, ISystemAlertService alertService, ILogger<MaintenanceJobService> logger)
    {
        _cacheService = cacheService;
        _healthService = healthService;
        _alertService = alertService;
        _logger = logger;
    }

    public async Task CheckRollbackAsync()
    {
        var expiresAtStr = await _cacheService.GetAsync<string>("maintenance:rollback_expires_at");
        if (string.IsNullOrEmpty(expiresAtStr)) return;

        var expiresAt = DateTime.Parse(expiresAtStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
        if (DateTime.UtcNow > expiresAt)
        {
            await _cacheService.RemoveAsync("maintenance:rollback_expires_at");
            await _cacheService.RemoveAsync("maintenance:rollback_failures");
            _logger.LogInformation("[Maintenance] Rollback monitoring window expired — no issues detected.");
            return;
        }

        try
        {
            var health = await _healthService.GetSystemHealthPulseAsync(default);
            var isUnhealthy = health.Status.Equals("Unhealthy", StringComparison.OrdinalIgnoreCase);
            var hasFailedJobs = health.JobPipeline.Failed > 3;

            if (isUnhealthy || hasFailedJobs)
            {
                var failures = await _cacheService.GetAsync<int>("maintenance:rollback_failures");
                failures++;
                await _cacheService.SetAsync("maintenance:rollback_failures", failures, TimeSpan.FromHours(1));

                _logger.LogWarning("[Maintenance] Rollback check: failure count {Count}/2 (Unhealthy={Unhealthy}, FailedJobs={FailedJobs})", failures, isUnhealthy, hasFailedJobs);

                if (failures >= 2)
                {
                    await _cacheService.SetAsync("maintenance:mode", true, null);
                    await _cacheService.SetAsync("maintenance:message", "Auto-rollback: System returned unhealthy after maintenance deactivation.", null);
                    await _cacheService.SetAsync("maintenance:activated_by", "System (Auto-Rollback)", null);
                    await _cacheService.SetAsync("maintenance:activated_at", DateTime.UtcNow.ToString("O"), null);

                    await _alertService.RaiseAlertAsync(
                        "Auto-Rollback: Maintenance Re-Enabled",
                        "System health degraded after maintenance deactivation. Maintenance has been automatically re-enabled.",
                        AlertSeverity.Critical,
                        "SYSTEM",
                        "System");

                    _logger.LogCritical("[Maintenance] AUTO-ROLLBACK TRIGGERED: Maintenance re-enabled due to health degradation.");
                    await _cacheService.RemoveAsync("maintenance:rollback_expires_at");
                    await _cacheService.RemoveAsync("maintenance:rollback_failures");
                }
            }
            else
            {
                await _cacheService.SetAsync("maintenance:rollback_failures", 0, TimeSpan.FromHours(1));
                _logger.LogInformation("[Maintenance] Rollback check passed — system healthy.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Maintenance] Rollback check failed.");
        }
    }
}
