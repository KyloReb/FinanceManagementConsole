using FMC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

public class CheckAuditLogs
{
    public static void Main(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Data Source=DESKTOP-7I5S48F;Initial Catalog=FMC_App;User ID=sa;Password=1234;TrustServerCertificate=True;MultipleActiveResultSets=True;Encrypt=True;Connect Timeout=30;")
            .Options;

        using (var context = new ApplicationDbContext(options, null))
        {
            var logs = context.AuditLogs.IgnoreQueryFilters().OrderByDescending(a => a.CreatedAt).Take(25).ToList();
            Console.WriteLine($"Found {logs.Count} logs in global space.");
            foreach (var log in logs)
            {
                Console.WriteLine($"Id: {log.Id} | Action: {log.Action} | Amount: {log.Amount} | TenantId: {log.TenantId} | PerformedBy: {log.PerformedBy} | Organization: {log.Organization} | EntityName: {log.EntityName}");
            }
        }
    }
}
