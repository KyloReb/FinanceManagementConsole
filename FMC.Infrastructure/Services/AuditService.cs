using FMC.Application.Interfaces;
using FMC.Domain.Entities;
using FMC.Shared.DTOs.Admin;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace FMC.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly IApplicationDbContext _context;
    private readonly Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;
    private readonly ISystemAlertService _alertService;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;

    public AuditService(IApplicationDbContext context, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager, ISystemAlertService alertService, Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _userManager = userManager;
        _alertService = alertService;
        _httpContextAccessor = httpContextAccessor;
    }

    private string GetDeviceName(string? ua)
    {
        if (string.IsNullOrEmpty(ua)) return "System/Internal";
        
        // If it was resolved as a Machine Name (started by our loopback logic in AuditService)
        if (!ua.Contains("Mozilla/") && !ua.Contains("Postman")) return ua;

        string os = "Other OS";
        if (ua.Contains("Windows NT 10.0", StringComparison.OrdinalIgnoreCase)) os = "Windows 10/11";
        else if (ua.Contains("Windows NT 6.3", StringComparison.OrdinalIgnoreCase)) os = "Windows 8.1";
        else if (ua.Contains("Windows NT 6.2", StringComparison.OrdinalIgnoreCase)) os = "Windows 8";
        else if (ua.Contains("Windows NT 6.1", StringComparison.OrdinalIgnoreCase)) os = "Windows 7";
        else if (ua.Contains("Android", StringComparison.OrdinalIgnoreCase)) os = "Android";
        else if (ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase)) os = "iOS (iPhone)";
        else if (ua.Contains("iPad", StringComparison.OrdinalIgnoreCase)) os = "iOS (iPad)";
        else if (ua.Contains("Macintosh", StringComparison.OrdinalIgnoreCase)) os = "macOS";
        else if (ua.Contains("Linux", StringComparison.OrdinalIgnoreCase)) os = "Linux";

        string browser = "Unknown Browser";
        if (ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) browser = "MS Edge";
        else if (ua.Contains("Chrome/", StringComparison.OrdinalIgnoreCase)) browser = "Chrome";
        else if (ua.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)) browser = "Firefox";
        else if (ua.Contains("Safari/", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Chrome", StringComparison.OrdinalIgnoreCase)) browser = "Safari";
        else if (ua.Contains("Postman", StringComparison.OrdinalIgnoreCase)) browser = "Postman";

        return $"{browser} on {os}";
    }

    public async Task RecordAuthEventAsync(string action, string? userId, string ipAddress, string device, string details)
    {
        // Intelligent Device Resolution: For loopback addresses (localhost/dev), resolve the actual machine name.
        // For remote traffic, fall back to the provided user-agent for forensic parsing.
        string resolvedDevice = device;
        if (ipAddress == "::1" || ipAddress == "127.0.0.1" || ipAddress == "localhost")
        {
            try { resolvedDevice = $"{Environment.MachineName} (Local)"; } catch { }
        }
        else
        {
            resolvedDevice = GetDeviceName(device);
        }

        if (action.Contains("Failed", StringComparison.OrdinalIgnoreCase))
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-15);
            var recentFailures = await _context.AuditLogs
                .CountAsync(a => a.IpAddress == ipAddress && a.Action.Contains("Failed") && a.CreatedAt >= cutoff);

            if (recentFailures >= 2)
            {
                await _alertService.RaiseAlertAsync(
                    "Identity Security Incident", 
                    $"Brute-force vector: Multiple authentication failures ({recentFailures + 1} attempts) detected from {ipAddress}. Target Account: {userId ?? "Anonymous"}. Boundary: {resolvedDevice}", 
                    AlertSeverity.Security, 
                    ipAddress, 
                    "Auth"
                );
            }
        }

        string tenantId = "SYSTEM";
        string? performedBy = null;
        string? entityName = null;

        if (!string.IsNullOrEmpty(userId))
        {
            var user = await _userManager.Users.Include(u => u.OrganizationInfo)
                .FirstOrDefaultAsync(u => u.Id == userId || u.UserName == userId);
            
            if (user != null)
            {
                performedBy = user.UserName;
                if (user.OrganizationInfo != null)
                {
                    tenantId = user.OrganizationInfo.Id.ToString();
                    entityName = user.OrganizationInfo.Name;
                }
                else
                {
                    entityName = user.Organization ?? "System";
                }
            }
            else 
            {
                 performedBy = userId;
                 tenantId = userId;
            }
        }

        var log = new AuditLog
        {
            UserId = userId,
            TenantId = tenantId,
            Action = action,
            IpAddress = ipAddress,
            Device = resolvedDevice,
            Details = details,
            CreatedAt = DateTime.UtcNow,
            PerformedBy = performedBy,
            EntityName = entityName
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task<List<AuditLogDto>> GetAuthLogsAsync()
    {
        // Now filtering for the new Action strings we might use, or all auth-related actions.
        var authActions = new[] { "Login Success", "Login Failed", "Logout", "Password Reset", "Password Changed", "Registration" };

        var logs = await _context.AuditLogs
            .IgnoreQueryFilters() // SuperAdmin needs to see global history
            .Where(a => a.Action == "Login" || authActions.Contains(a.Action)) // Keep "Login" for backward compatibility
            .OrderByDescending(a => a.CreatedAt)
            .Take(500)
            .ToListAsync();

        var dtos = new List<AuditLogDto>();
        foreach (var log in logs)
        {
            var user = log.UserId != null && log.UserId != "current" 
                ? await _userManager.Users.Include(u => u.OrganizationInfo)
                    .FirstOrDefaultAsync(u => u.Id == log.UserId || u.UserName == log.UserId) 
                : null;
                
            dtos.Add(new AuditLogDto
            {
                Id = log.Id,
                UserId = log.UserId,
                UserName = user?.UserName ?? "Unknown",
                Action = log.Action,
                IpAddress = log.IpAddress,
                Device = log.Device,
                Organization = !string.IsNullOrWhiteSpace(user?.OrganizationInfo?.Name) 
                    ? user.OrganizationInfo.Name 
                    : (!string.IsNullOrWhiteSpace(user?.Organization) ? user.Organization : (user != null ? "N/A" : "Guest/System")),
                Details = log.Details,
                CreatedAt = log.CreatedAt
            });
        }
        return dtos;
    }

    public async Task RecordFinancialEventAsync(string action, Guid entityId, string entityName, decimal amount, string label, string performedBy, string? details = null, string? tenantId = null)
    {
        var rawIp = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "System";
        if (rawIp == "::1" || rawIp == "127.0.0.1") rawIp = "127.0.0.1 (Localhost)";
        var rawUserAgent = _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString();
        var resolvedDevice = string.IsNullOrEmpty(rawUserAgent) ? "System Initiated" : GetDeviceName(rawUserAgent);

        var log = new AuditLog
        {
            Action = action,
            EntityType = "Organization",
            EntityId = entityId.ToString(),
            EntityName = entityName,
            Amount = amount,
            Label = label,
            PerformedBy = performedBy,
            Details = details,
            CreatedAt = DateTime.UtcNow,
            TenantId = tenantId ?? "FINANCIAL",
            IpAddress = rawIp,
            Device = resolvedDevice
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task<List<AuditLogDto>> GetRecentLogsAsync(int count = 20, string? category = null, string? tenantId = null)
    {
        IQueryable<AuditLog> query = _context.AuditLogs.IgnoreQueryFilters().OrderByDescending(a => a.CreatedAt);

        if (!string.IsNullOrEmpty(tenantId))
        {
            query = query.Where(a => a.TenantId == tenantId);
        }
        
        if (category == "financial")
        {
            query = query.Where(a => a.Amount != null || a.TenantId == "FINANCIAL");
        }

        var logs = await query.Take(count).ToListAsync();
        return logs.Select(log => new AuditLogDto
        {
            Id = log.Id,
            Action = log.Action,
            EntityName = log.EntityName,
            Amount = log.Amount,
            Label = log.Label,
            PerformedBy = log.PerformedBy,
            CreatedAt = log.CreatedAt,
            Details = log.Details,
            IpAddress = log.IpAddress,
            Device = log.Device
        }).ToList();
    }

    public async Task<AuditLogSearchResultDto> SearchLogsAsync(AuditLogQueryDto queryDto)
    {
        var dbQuery = _context.AuditLogs.IgnoreQueryFilters().AsQueryable();

        if (!string.IsNullOrEmpty(queryDto.TenantId))
            dbQuery = dbQuery.Where(a => a.TenantId == queryDto.TenantId);
            
        if (queryDto.Category == "financial")
            dbQuery = dbQuery.Where(a => a.Amount != null || a.TenantId == "FINANCIAL");
        else if (queryDto.Category == "auth")
            dbQuery = dbQuery.Where(a => a.Amount == null && a.TenantId != "FINANCIAL");

        if (!string.IsNullOrEmpty(queryDto.Action))
            dbQuery = dbQuery.Where(a => a.Action.Contains(queryDto.Action));

        if (!string.IsNullOrEmpty(queryDto.PerformedBy))
            dbQuery = dbQuery.Where(a => a.PerformedBy != null && a.PerformedBy.Contains(queryDto.PerformedBy));

        if (!string.IsNullOrEmpty(queryDto.EntityName))
            dbQuery = dbQuery.Where(a => a.EntityName != null && a.EntityName.Contains(queryDto.EntityName));

        if (queryDto.FromDate.HasValue)
            dbQuery = dbQuery.Where(a => a.CreatedAt >= queryDto.FromDate.Value);

        if (queryDto.ToDate.HasValue)
            dbQuery = dbQuery.Where(a => a.CreatedAt <= queryDto.ToDate.Value);

        var total = await dbQuery.CountAsync();
        
        var logs = await dbQuery
            .OrderByDescending(a => a.CreatedAt)
            .Skip((queryDto.Page - 1) * queryDto.PageSize)
            .Take(queryDto.PageSize)
            .ToListAsync();

        return new AuditLogSearchResultDto
        {
            TotalCount = total,
            Items = logs.Select(log => new AuditLogDto
            {
                Id = log.Id,
                Action = log.Action,
                EntityName = log.EntityName,
                Amount = log.Amount,
                Label = log.Label,
                PerformedBy = log.PerformedBy,
                CreatedAt = log.CreatedAt,
                Details = log.Details,
                IpAddress = log.IpAddress,
                Device = log.Device,
                UserId = log.UserId
            }).ToList()
        };
    }
}
