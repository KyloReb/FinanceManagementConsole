using FMC.Application.Interfaces;
using FMC.Domain.Entities;
using FMC.Shared.DTOs.Admin;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FMC.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly IApplicationDbContext _context;
    private readonly Microsoft.AspNetCore.Identity.UserManager<FMC.Infrastructure.Data.ApplicationUser> _userManager;

    public AuditService(IApplicationDbContext context, Microsoft.AspNetCore.Identity.UserManager<FMC.Infrastructure.Data.ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
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

        var log = new AuditLog
        {
            UserId = userId,
            TenantId = userId ?? "SYSTEM", 
            Action = action,
            IpAddress = ipAddress,
            Device = resolvedDevice,
            Details = details,
            CreatedAt = DateTime.UtcNow
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
}
