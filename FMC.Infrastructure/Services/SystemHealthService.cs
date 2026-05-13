using FMC.Application.Interfaces;
using FMC.Shared.DTOs.Admin;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FMC.Infrastructure.Services;

/// <summary>
/// Infrastructure-level implementation of the System Health Service.
/// This service acts as an aggregator, pulling data from the native .NET Health Check system,
/// the Hangfire Monitoring API, and system process telemetry.
/// </summary>
public class SystemHealthService : ISystemHealthService
{
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger<SystemHealthService> _logger;
    private static readonly DateTime _startTime = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    public SystemHealthService(
        HealthCheckService healthCheckService,
        ILogger<SystemHealthService> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    public async Task<SystemHealthDto> GetSystemHealthPulseAsync(CancellationToken cancellationToken = default)
    {
        // 1. Run native .NET Health Checks (DB, SMTP, etc.)
        var healthReport = await _healthCheckService.CheckHealthAsync(cancellationToken);
        
        var dto = new SystemHealthDto
        {
            Status = healthReport.Status.ToString(),
            StatusColor = GetStatusColor(healthReport.Status),
            Uptime = DateTime.UtcNow - _startTime,
            Infrastructure = healthReport.Entries.Select(entry => new HealthMetricDto
            {
                Name = entry.Key,
                Status = entry.Value.Status.ToString(),
                LatencyMs = $"{entry.Value.Duration.TotalMilliseconds:F0}ms",
                Details = entry.Value.Description
            }).ToList()
        };

        // 2. Aggregate Hangfire Job Pipeline Metrics
        try
        {
            var monitor = JobStorage.Current.GetMonitoringApi();
            var statistics = monitor.GetStatistics();

            dto.JobPipeline = new JobPipelineDto
            {
                Active = (int)statistics.Processing,
                Scheduled = (int)statistics.Scheduled,
                Succeeded = (int)statistics.Succeeded,
                Failed = (int)statistics.Failed,
                Queued = (int)statistics.Enqueued
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve Hangfire statistics for Health Pulse.");
            dto.JobPipeline = new JobPipelineDto { Status = "Monitoring Offline" }; // Custom field or handled by UI
        }

        // 3. Collect System Process Telemetry
        var process = Process.GetCurrentProcess();
        dto.Performance = new PerformanceTelemetryDto
        {
            MemoryUsageMb = process.WorkingSet64 / (1024 * 1024),
            CpuUsagePct = GetCpuUsage(process),
            AvgRequestLatencyMs = healthReport.TotalDuration.TotalMilliseconds,
            TrafficVolume = GetTrafficVolumeIndicator(dto.JobPipeline.Active)
        };

        return dto;
    }

    private string GetStatusColor(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "#00d2be",   // Emerald Teal (Vibrant)
        HealthStatus.Degraded => "#ffb300",  // Deep Amber
        HealthStatus.Unhealthy => "#ff3d00", // Radiant Crimson
        _ => "#b2bec3"                        // Silver Grey
    };

    private double GetCpuUsage(Process process)
    {
        // Note: Real CPU usage tracking usually requires a PerformanceCounter or multiple samples.
        // This is a simplified "Total Processor Time" estimate for the dashboard preview.
        try { return Math.Min(100, (process.TotalProcessorTime.TotalMilliseconds / (DateTime.UtcNow - _startTime).TotalMilliseconds) * 100); }
        catch { return 0; }
    }

    private string GetTrafficVolumeIndicator(int activeJobs) => activeJobs switch
    {
        0 => "Idle",
        < 5 => "Nominal",
        < 20 => "Elevated",
        _ => "High Load"
    };
}
