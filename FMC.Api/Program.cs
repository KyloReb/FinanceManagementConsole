using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FMC.Infrastructure.Authentication;
using FMC.Infrastructure.Data;
using FMC.Domain.Entities;
using FMC.Application.Interfaces;
using FMC.Application.Transactions.Queries;
using FMC.Infrastructure.Services;
using FMC.Infrastructure.Caching;
using FMC.Infrastructure.Repositories;
using FMC.Infrastructure.Resilience;
using FMC.Infrastructure.BackgroundJobs;
using FMC.Shared.Auth;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.StackExchangeRedis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
#region Culture Configuration
var cultureInfo = new System.Globalization.CultureInfo("en-PH");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
#endregion

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = CookieSecurePolicy.Always;
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
});
// Caching Configuration
if (builder.Environment.IsDevelopment())
{
    // Use Memory Cache for easy local development without needing Redis
    builder.Services.AddDistributedMemoryCache();
}
else
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("RedisConnection");
    });
}

// ─────────────────────────────────────────────────────────────
// Hangfire Background Job Infrastructure
// Uses the same SQL Server DB — zero additional infrastructure cost.
// Jobs persist across restarts and are retried automatically on failure.
// ─────────────────────────────────────────────────────────────
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout       = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout   = TimeSpan.FromMinutes(5),
            QueuePollInterval            = TimeSpan.Zero, // Immediately processes jobs
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks           = true
        }));

// Register the Hangfire background processing server
// WorkerCount: 2 workers is ideal for a company server; increase if email volume grows.
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2;
    options.ServerName  = "FMC-BackgroundWorker";
    options.Queues      = new[] { "critical", "default", "low" };
});

// Authentication Settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // LOCKOUT SETTINGS (Anti-Brute Force)
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() 
                 ?? throw new InvalidOperationException("JwtSettings are missing.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
            {
                context.Response.Headers.Append("Token-Expired", "true");
            }
            // Log the actual reason for 401 in the API console
            Console.WriteLine($"[JWT-DEBUG] Authentication Failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine($"[JWT-DEBUG] Token Validated for: {context.Principal?.Identity?.Name}");
            return Task.CompletedTask;
        }
    };
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
    };
});

// RBAC Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Roles.CEO, policy => policy.RequireRole(Roles.CEO, Roles.SuperAdmin));
    options.AddPolicy(Roles.Maker, policy => policy.RequireRole(Roles.Maker));
    options.AddPolicy(Roles.Approver, policy => policy.RequireRole(Roles.Approver));
});

// Database with built-in EF Core transient fault retry (SQL Server)
// EnableRetryOnFailure automatically retries up to 5 times on known transient SQL errors.
// This is the first layer of resilience — Polly adds a second layer on top.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));

// DI for Layers
builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<IAuditService, AuditService>();
// Resilience: Register the concrete repository (for the decorator to inject)
// and then override the interface binding with the resilient decorator.
// This means every time IOrganizationRepository is requested, the system gets
// a version that automatically retries on transient database faults.
builder.Services.AddScoped<OrganizationRepository>();
builder.Services.AddScoped<IOrganizationRepository, ResilientOrganizationRepository>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ICacheService, RedisCacheService>();
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<ISystemAlertService, SystemAlertService>();

// Background Job Services
// NotificationJobService is the typed job class that Hangfire instantiates in its own DI scope.
// IBackgroundJobService is the clean abstraction used by the notification handler.
builder.Services.AddScoped<NotificationJobService>();
builder.Services.AddScoped<IBackgroundJobService, HangfireBackgroundJobService>();
builder.Services.AddHostedService<FMC.Infrastructure.BackgroundServices.HealthMonitorService>();

// MediatR
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssemblies(
        typeof(GetRecentTransactionsQuery).Assembly,
        typeof(OrganizationNotificationHandler).Assembly);
});

var app = builder.Build();

// Automatic Database Synchronization for Multi-Tenancy (TenantId) and Forensics (Device)
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    // 1. Synchronize TenantId for all core tables
    var tables = new[] { "Accounts", "Transactions", "Budgets", "AuditLogs" };
    foreach (var table in tables)
    {
        try 
        {
            string sql = $"IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[{table}]') AND name = N'TenantId') " +
                         $"BEGIN ALTER TABLE [{table}] ADD [TenantId] nvarchar(max) NOT NULL DEFAULT N''; END";
            await db.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error synchronizing TenantId for table {Table}", table);
        }
    }

    // 2. Synchronize OrganizationId for Accounts and Transactions
    var orgSyncTables = new[] { "Accounts", "Transactions" };
    foreach (var table in orgSyncTables)
    {
        try
        {
            string sql = $"IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[{table}]') AND name = N'OrganizationId') " +
                         $"BEGIN ALTER TABLE [{table}] ADD [OrganizationId] uniqueidentifier NULL; END";
            await db.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error synchronizing OrganizationId for table {Table}", table);
        }
    }

    // 3. Synchronize Device (Forensics) specifically for AuditLogs
    try
    {
        string deviceSql = "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[AuditLogs]') AND name = N'Device') " +
                           "BEGIN ALTER TABLE [AuditLogs] ADD [Device] nvarchar(max) NULL; END";
        await db.Database.ExecuteSqlRawAsync(deviceSql);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error synchronizing Device column for AuditLogs");
    }

    // 3. Synchronize Organization for AspNetUsers
    try
    {
        string orgSql = "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[AspNetUsers]') AND name = N'Organization') " +
                         "BEGIN ALTER TABLE [AspNetUsers] ADD [Organization] nvarchar(max) NULL; END";
        await db.Database.ExecuteSqlRawAsync(orgSql);

        // Update existing users to Nationlink/Infoserve Inc. as requested
        string updateSql = "UPDATE [AspNetUsers] SET [Organization] = N'Nationlink/Infoserve Inc.' WHERE [Organization] IS NULL";
        await db.Database.ExecuteSqlRawAsync(updateSql);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error synchronizing Organization column for AspNetUsers");
    }

    // 4. Seed Essential Identity Roles and SuperAdmin CEO Account
    try
    {
        await ApplicationDbSeeder.SeedRolesAndAdminAsync(scope.ServiceProvider);
        logger.LogInformation("Database seeding completed successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding the database roles and admin.");
    }
}

// Configure the HTTP request pipeline.
// Always enable Swagger during this architectural transition phase
app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseCookiePolicy();

app.UseAuthentication();
app.UseAuthorization();

// Hangfire Dashboard — accessible at /hangfire
// Provides real-time visibility into all background jobs: queued, processing, succeeded, failed.
// IMPORTANT: In production, secure this endpoint with IP filtering or an admin role policy.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    // Allow viewing the dashboard without authentication during development.
    // In production: replace with a custom IAuthorizationFilter that checks for SuperAdmin role.
    Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
});

app.MapControllers();

app.Run();

