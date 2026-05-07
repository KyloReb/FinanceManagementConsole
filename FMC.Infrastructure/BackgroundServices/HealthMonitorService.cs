using FMC.Application.Interfaces;
using FMC.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FMC.Infrastructure.BackgroundServices;

public class HealthMonitorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HealthMonitorService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);

    public HealthMonitorService(IServiceProvider serviceProvider, ILogger<HealthMonitorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("System Health Monitor started...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var orgService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
                    var alertService = scope.ServiceProvider.GetRequiredService<ISystemAlertService>();
                    
                    var orgs = await orgService.GetAllAsync(stoppingToken);

                    foreach (var org in orgs)
                    {
                        var remainingBalance = org.TotalBalance - org.Usage;

                        // Business Rule: Active tenants must have positive liquidity
                        if (remainingBalance <= 0 && org.IsActive)
                        {
                            await alertService.RaiseAlertAsync(
                                "Critical Liquidity Threshold", 
                                $"Tenant '{org.Name}' has depleted its wallet ({remainingBalance:C}). Background health check auto-flagged.", 
                                AlertSeverity.Critical, 
                                org.Id.ToString(), 
                                "Organization"
                            );
                        }
                        else
                        {
                            // Auto-resolve if balance is restored
                            await alertService.ResolveAlertAsync("Critical Liquidity Threshold", org.Id.ToString());
                        }

                        // Business Rule: Tenants should have a designated CEO
                        if (string.IsNullOrEmpty(org.CeoName) && org.IsActive)
                        {
                            await alertService.RaiseAlertAsync(
                                "Governance Violation", 
                                $"Organization '{org.Name}' has no designated Chief Executive (CEO).", 
                                AlertSeverity.Warning, 
                                org.Id.ToString(), 
                                "Organization"
                            );
                        }
                        else if (!string.IsNullOrEmpty(org.CeoName))
                        {
                            // Auto-resolve if CEO is assigned
                            await alertService.ResolveAlertAsync("Governance Violation", org.Id.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "System Health Monitor failed during execution.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}
