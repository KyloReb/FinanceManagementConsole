using Microsoft.EntityFrameworkCore;
using FMC.Infrastructure.Data;
using FMC.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=FMC;Trusted_Connection=True;MultipleActiveResultSets=true"));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

var users = await db.Users.ToListAsync();
Console.WriteLine($"Found {users.Count} users:");
foreach (var user in users)
{
    var roles = await userManager.GetRolesAsync(user);
    Console.WriteLine($"- {user.UserName} ({user.Email}): IsActive={user.IsActive}, Roles={string.Join(", ", roles)}");
}
