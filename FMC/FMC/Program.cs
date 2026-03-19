using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using FMC.Data;
using FMC.Client.Pages;
using FMC.Components;
using MudBlazor.Services;
using FMC.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
#region Data Access Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Safely provide a Scoped ApplicationDbContext using the Factory to satisfy ASP.NET Identity
builder.Services.AddScoped<ApplicationDbContext>(p => 
    p.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
#endregion

#region Authentication & Identity Configuration
builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies(options =>
    {
        options.ApplicationCookie.Configure(cookieOptions =>
        {
            cookieOptions.LoginPath = "/login";
            cookieOptions.AccessDeniedPath = "/not-found";
        });
    });

builder.Services.AddIdentityCore<ApplicationUser>(options => 
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
    
builder.Services.AddAuthorizationBuilder();
#endregion

#region Blazor & Web UI Configuration
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddMudServices();
builder.Services.AddControllers();
#endregion

#region Custom Application Services
builder.Services.AddScoped<IFinanceService, FinanceService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<ThemeService>();
#endregion

var app = builder.Build();

#region Seeding Operations
// Seed Identity Roles and Admin User
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // Run the seeder synchronously to ensure roles exist before the app starts accepting requests.
        ApplicationDbSeeder.SeedRolesAndAdminAsync(services).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the Identity database.");
    }
}
#endregion

// Configure the HTTP request pipeline.
#region HTTP Request Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(FMC.Client._Imports).Assembly);

// Seed the database (Commented out as requested - DB will start empty)
// using (var scope = app.Services.CreateScope())
// {
//     var services = scope.ServiceProvider;
//     var context = services.GetRequiredService<ApplicationDbContext>();
//     await DbInitializer.SeedData(context);
// }
#endregion

app.Run();
