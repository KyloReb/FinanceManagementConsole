using Microsoft.AspNetCore.Identity;

namespace FMC.Data;

/// <summary>
/// Handles initializing the database with essential Identity roles and a default SuperAdmin account.
/// </summary>
public static class ApplicationDbSeeder
{
    /// <summary>
    /// Seeds the default roles and an initial SuperAdmin user if they do not already exist.
    /// </summary>
    /// <param name="serviceProvider">The scoped service provider to resolve Identity managers.</param>
    public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // 1. Seed Roles
        string[] roleNames = { "SuperAdmin", "Admin", "User", "Viewer" };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // 2. Seed Initial SuperAdmin
        var adminEmail = "davidrebancos02@gmail.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = "admin", // This is what you type in the login box
                Email = adminEmail, // This is where OTPs and resets go
                FirstName = "System",
                LastName = "Admin",
                EmailConfirmed = true,
                IsActive = true
            };

            // Setup with a strong default password
            var createPowerUser = await userManager.CreateAsync(adminUser, "SuperAdmin123!");
            if (createPowerUser.Succeeded)
            {
                // Assign the highest privilege role
                await userManager.AddToRoleAsync(adminUser, "SuperAdmin");
                Console.WriteLine("SEEDER: Successfully created admin user.");
            }
            else
            {
                Console.WriteLine("SEEDER ERROR: Failed to create admin user: " + string.Join(" | ", createPowerUser.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            // If the user already exists from a very early migration phase where password was '1234',
            // forcing a sync here ensures the requested 'SuperAdmin123!' password takes effect!
            var isExpectedPassword = await userManager.CheckPasswordAsync(adminUser, "SuperAdmin123!");
            if (!isExpectedPassword)
            {
                var removeResult = await userManager.RemovePasswordAsync(adminUser);
                if (removeResult.Succeeded)
                {
                    await userManager.AddPasswordAsync(adminUser, "SuperAdmin123!");
                }
            }
            
            // Just in case they got locked out while testing
            await userManager.SetLockoutEndDateAsync(adminUser, null);
            await userManager.ResetAccessFailedCountAsync(adminUser);
        }
    }
}
