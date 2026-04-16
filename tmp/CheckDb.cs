using FMC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;

// This script checks the Accounts table to understand how TenantId is being mapped to users.
// We need this to correctly populate the 'Available balance' in the profile.

public class DatabaseCheck
{
    public static void Main(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=FMC;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        using (var context = new ApplicationDbContext(options, null))
        {
            var accounts = context.Accounts.IgnoreQueryFilters().ToList();
            Console.WriteLine($"Found {accounts.Count} accounts in global space.");
            foreach (var account in accounts)
            {
                Console.WriteLine($"Account: {account.Name} | Balance: {account.Balance} | TenantId: {account.TenantId}");
            }
            
            var users = context.Users.ToList();
            Console.WriteLine($"\nFound {users.Count} users.");
            foreach (var user in users)
            {
                Console.WriteLine($"User: {user.UserName} | Id: {user.Id} | OrgId: {user.OrganizationId}");
            }
        }
    }
}
