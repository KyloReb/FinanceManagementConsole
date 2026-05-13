using System;
using System.Collections.Generic;

namespace FMC.Shared.DTOs.Admin;

/// <summary>
/// High-level diagnostic snapshot of the system's infrastructure health.
/// Used by SuperAdmins to monitor operational stability and background job performance.
/// </summary>
public class SystemHealthDto
{
    /// <summary>Overall status: "Healthy", "Degraded", or "Unhealthy".</summary>
    public string Status { get; set; } = "Unknown";
    
    /// <summary>Color code for the pulse indicator (Hex or HSL).</summary>
    public string StatusColor { get; set; } = "#b2bec3";

    /// <summary>The total time the system has been running since last restart.</summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>Infrastructure-level metrics (Database, SMTP, Storage).</summary>
    public List<HealthMetricDto> Infrastructure { get; set; } = new();

    /// <summary>Real-time background job statistics from Hangfire.</summary>
    public JobPipelineDto JobPipeline { get; set; } = new();

    /// <summary>Performance telemetry (Avg Latency, CPU, RAM usage).</summary>
    public PerformanceTelemetryDto Performance { get; set; } = new();
}

public class HealthMetricDto
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown";
    public string LatencyMs { get; set; } = "0ms";
    public string? Details { get; set; }
}

public class JobPipelineDto
{
    public int Active { get; set; }
    public int Scheduled { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Queued { get; set; }
    public string? Status { get; set; }
}

public class PerformanceTelemetryDto
{
    public double CpuUsagePct { get; set; }
    public double MemoryUsageMb { get; set; }
    public double AvgRequestLatencyMs { get; set; }
    public string TrafficVolume { get; set; } = "Low";
}
