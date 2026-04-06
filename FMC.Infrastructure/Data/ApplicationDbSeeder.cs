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

        // 1. Seed Roles (SuperAdmin, CEO, Manager, User)
        string[] roleNames = { Roles.SuperAdmin, Roles.CEO, Roles.Manager, Roles.User };
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
            if (adminUser.OrganizationId == null || adminUser.OrganizationId != primaryOrg.Id)
            {
                adminUser.OrganizationId = primaryOrg.Id;
                adminUser.Organization = primaryOrg.Name;
                await userManager.UpdateAsync(adminUser);
            }
        }
    }
}
