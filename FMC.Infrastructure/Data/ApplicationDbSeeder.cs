using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using FMC.Shared.Auth;

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

        // 2. Seed Initial CEO
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
                IsActive = true
            };

            var createPowerUser = await userManager.CreateAsync(adminUser, "SuperAdmin123!");
            if (createPowerUser.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, Roles.SuperAdmin);
                await userManager.AddToRoleAsync(adminUser, Roles.CEO);
            }
        }
    }
}
