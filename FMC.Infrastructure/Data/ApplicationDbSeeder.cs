using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using FMC.Shared.Auth;
using FMC.Domain.Entities;

namespace FMC.Infrastructure.Data;

/// <summary>
/// Handles initializing the database with essential Identity roles and a default CEO account.
/// </summary>
public static class ApplicationDbSeeder
{
    /// <summary>
    /// Seeds the default roles and an initial CEO user if they do not already exist.
    /// </summary>
    /// <param name="serviceProvider">The scoped service provider to resolve Identity managers.</param>
    public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // 1. Seed Roles (SuperAdmin, CEO, Maker, Approver, User)
        string[] roleNames = { Roles.SuperAdmin, Roles.CEO, Roles.Maker, Roles.Approver, Roles.User };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // 2. Seed Primary Organization
        var db = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var primaryOrg = await db.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Name == "Nationlink/Infoserve Inc.");

        if (primaryOrg == null)
        {
            primaryOrg = new Organization
            {
                Name = "Nationlink/Infoserve Inc.",
                Description = "Primary System Administrator Organization (Unmodifiable)",
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                TenantId = "SYSTEM"
            };
            db.Organizations.Add(primaryOrg);
            await db.SaveChangesAsync();
        }

        // 3. Seed Initial SuperAdmin
        var adminEmail = "davidrebancos02@gmail.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = "admin", 
                Email = adminEmail, 
                FirstName = "System",
                LastName = "CEO",
                EmailConfirmed = true,
                IsActive = true,
                Organization = primaryOrg.Name,
                OrganizationId = primaryOrg.Id
            };

            var createPowerUser = await userManager.CreateAsync(adminUser, "SuperAdmin123!");
            if (createPowerUser.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, Roles.SuperAdmin);
            }
        }
        else
        {
            // Link existing admin to the organization if not already linked
            bool needsUpdate = false;
            if (!adminUser.IsActive) { adminUser.IsActive = true; needsUpdate = true; }
            if (adminUser.OrganizationId == null || adminUser.OrganizationId != primaryOrg.Id)
            {
                adminUser.OrganizationId = primaryOrg.Id;
                adminUser.Organization = primaryOrg.Name;
                needsUpdate = true;
            }
            if (needsUpdate) await userManager.UpdateAsync(adminUser);

            if (!await userManager.IsInRoleAsync(adminUser, Roles.SuperAdmin))
            {
                await userManager.AddToRoleAsync(adminUser, Roles.SuperAdmin);
            }
        }

        // 4. Specifically activate 'david' if he exists, to facilitate user testing
        var david = await userManager.FindByNameAsync("david") ?? await userManager.FindByEmailAsync("david.rebancos@nationlink.ph");
        if (david != null && !david.IsActive)
        {
            david.IsActive = true;
            await userManager.UpdateAsync(david);
        }

        // 5. Seed 5 Cardholders per Organization
        var organizations = await db.Organizations.IgnoreQueryFilters().ToListAsync();
        
        string[] fNames = {"Emma", "Liam", "Olivia", "Noah", "Ava", "Elijah", "Sophia", "William"};
        string[] lNames = {"Garcia", "Martinez", "Rodriguez", "Lopez", "Gonzalez", "Perez", "Sanchez", "Ramirez"};
        int nameIndex = 0;

        foreach (var org in organizations)
        {
            for (int i = 1; i <= 5; i++)
            {
                var email = $"cardholder{i}@{org.Name.Replace(" ", "").Replace("/", "").ToLower()}.local";
                var cardholder = await db.Cardholders.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Email == email);
                
                var fn = fNames[nameIndex % fNames.Length];
                var ln = lNames[nameIndex % lNames.Length];

                if (cardholder == null)
                {
                    cardholder = new Cardholder
                    {
                        Id = Guid.NewGuid(),
                        FirstName = fn,
                        LastName = ln,
                        Email = email,
                        IsActive = true,
                        OrganizationId = org.Id,
                        TenantId = org.Id.ToString(),
                        
                        // --- SEED GENERATION ---
                        // Comment out this line or replace with existing data if needed
                        AccountNumber = "63641" + new Random().NextInt64(10000000000, 99999999999).ToString(),
                        
                        CreatedAt = DateTime.UtcNow
                    };

                    db.Cardholders.Add(cardholder);
                    
                    // Create Wallet for Cardholder
                    var account = new Account
                    {
                        Id = Guid.NewGuid(),
                        Name = $"Wallet: {fn} {ln}",
                        Balance = 0,
                        TenantId = cardholder.Id.ToString(), // Link to Cardholder ID
                        OrganizationId = org.Id
                    };
                    db.Accounts.Add(account);
                }
                else
                {
                    // Existing cardholder update
                    if (cardholder.FirstName != fn || cardholder.LastName != ln)
                    {
                        cardholder.FirstName = fn;
                        cardholder.LastName = ln;
                        db.Cardholders.Update(cardholder);

                        var account = await db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.TenantId == cardholder.Id.ToString());
                        if (account != null && account.Name.StartsWith("Wallet:"))
                        {
                            account.Name = $"Wallet: {fn} {ln}";
                            db.Accounts.Update(account);
                        }
                    }
                }
                nameIndex++;
            }
        }
        // 6. Cleanup: Remove legacy Wallets/Accounts for administrative staff (non-cardholders)
        var staffRoles = new[] { Roles.SuperAdmin, Roles.CEO, Roles.Maker, Roles.Approver };
        var allStaffUsers = new List<ApplicationUser>();
        foreach (var roleName in staffRoles)
        {
            var members = await userManager.GetUsersInRoleAsync(roleName);
            allStaffUsers.AddRange(members);
        }

        var staffUserIds = allStaffUsers.Select(u => u.Id).Distinct().ToList();
        if (staffUserIds.Any())
        {
            // Remove any Account/Wallet record linked to a staff member's ID
            var accountsToPurge = await db.Accounts.IgnoreQueryFilters()
                .Where(a => staffUserIds.Contains(a.TenantId))
                .ToListAsync();

            if (accountsToPurge.Any())
            {
                db.Accounts.RemoveRange(accountsToPurge);
            }
        }

        await db.SaveChangesAsync();
    }
}

