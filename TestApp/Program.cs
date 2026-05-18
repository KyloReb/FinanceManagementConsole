using FMC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

public class Program
{
    public static void Main(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Data Source=DESKTOP-7I5S48F;Initial Catalog=FMC_App;User ID=sa;Password=1234;TrustServerCertificate=True;MultipleActiveResultSets=True;Encrypt=True;Connect Timeout=30;")
            .Options;

        using (var context = new ApplicationDbContext(options, null))
        {
            var orgs = context.Organizations.IgnoreQueryFilters().ToList();
            Console.WriteLine($"Found {orgs.Count} organizations. Checking for missing account numbers...");
            bool updated = false;
            foreach (var org in orgs)
            {
                if (org.Name.Contains("Nationlink"))
                {
                    Console.WriteLine($"-> Skipping admin organization: {org.Name}");
                    continue; // exclude Nationlink
                }

                if (string.IsNullOrWhiteSpace(org.AccountNumber) || org.AccountNumber == "N/A")
                {
                    var rnd = new Random(Guid.NewGuid().GetHashCode());
                    long r1 = rnd.Next(10000, 99999);
                    long r2 = rnd.Next(100000, 999999);
                    org.AccountNumber = "63641" + r1.ToString("D5") + r2.ToString("D6");
                    Console.WriteLine($"-> Assigned random AccountNumber '{org.AccountNumber}' to '{org.Name}'");
                    context.Organizations.Update(org);
                    updated = true;
                }
            }
            if (updated)
            {
                context.SaveChanges();
                Console.WriteLine("Database updated successfully!");
            }
            else
            {
                Console.WriteLine("All organizations already have account numbers!");
            }
        }
    }
}
