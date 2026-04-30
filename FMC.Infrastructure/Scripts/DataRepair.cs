using Microsoft.EntityFrameworkCore;
using FMC.Infrastructure.Data;
using FMC.Domain.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using FMC.Application.Interfaces;

namespace FMC.Infrastructure.Scripts;

public class DataRepair
{
    public static async Task AlignLegacyIds(ApplicationDbContext context)
    {
        Console.WriteLine("Starting ID Alignment...");
        var cardholders = await context.Cardholders.IgnoreQueryFilters().ToListAsync();
        int updated = 0;
        
        foreach (var c in cardholders)
        {
            if (string.IsNullOrEmpty(c.IdentityUserId)) continue;

            // Find account that is still linked to the old IdentityUserId
            var legacyAccount = await context.Accounts.IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.TenantId == c.IdentityUserId);

            if (legacyAccount != null)
            {
                Console.WriteLine($"Found legacy account for {c.FirstName} {c.LastName}. Aligning...");
                
                // Update Account to use the new Cardholder ID
                legacyAccount.TenantId = c.Id.ToString();
                
                // Update all associated transactions too
                var transactions = await context.Transactions.IgnoreQueryFilters()
                    .Where(t => t.TenantId == c.IdentityUserId)
                    .ToListAsync();
                
                foreach(var t in transactions)
                {
                    t.TenantId = c.Id.ToString();
                }
                
                updated++;
            }
        }

        if (updated > 0)
        {
            await context.SaveChangesAsync();
            Console.WriteLine($"Successfully aligned {updated} legacy accounts.");
        }
        else
        {
            Console.WriteLine("No legacy accounts found needing alignment.");
        }
    }
}
