using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;
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
using FMC.Infrastructure.Diagnostics;
using FMC.Shared.Auth;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
#region Culture Configuration
var cultureInfo = new System.Globalization.CultureInfo("en-PH");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
#endregion

builder.Services.AddControllers(options =>
{
    options.Filters.AddService<FMC.Api.Filters.IdempotencyFilter>();
});
builder.Services.AddScoped<FMC.Api.Filters.IdempotencyFilter>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddExceptionHandler<FMC.Api.Middleware.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// 1. CORS Configuration (P1 #4)
var allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() 
                     ?? new[] { "https://localhost:7027" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("FmcCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// 2. Rate Limiting Configuration (Enterprise — Sliding Window + Progressive Backoff)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddSlidingWindowLimiter("AuthPolicy", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.SegmentsPerWindow = 4;
        limiterOptions.QueueLimit = 0;
    });
});
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = CookieSecurePolicy.SameAsRequest;
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
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
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
    options.AddPolicy(Roles.SuperAdminApprover, policy => policy.RequireRole(Roles.SuperAdminApprover, Roles.Approver));
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
builder.Services.AddScoped<IAuthRateLimitService, AuthRateLimitService>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<ISystemAlertService, SystemAlertService>();
builder.Services.AddScoped<IExcelParserService, FMC.Infrastructure.Services.ExcelParserService>();
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();
builder.Services.AddScoped<ISystemHealthService, SystemHealthService>();

// Infrastructure Diagnostics & Monitoring
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("Financial Database", tags: new[] { "Database" })
    .AddCheck("Self", () => HealthCheckResult.Healthy("FMC API is online"), tags: new[] { "Liveness" });

// Background Job Services
// NotificationJobService is the typed job class that Hangfire instantiates in its own DI scope.
// IBackgroundJobService is the clean abstraction used by the notification handler.
builder.Services.AddScoped<NotificationJobService>();
builder.Services.AddScoped<MaintenanceJobService>();
builder.Services.AddScoped<IBackgroundJobService, HangfireBackgroundJobService>();
builder.Services.AddHostedService<FMC.Infrastructure.BackgroundServices.HealthMonitorService>();

// MediatR
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssemblies(
        typeof(GetRecentTransactionsQuery).Assembly,
        typeof(OrganizationNotificationHandler).Assembly);
});

var app = builder.Build();

// Database schema synchronization and seeding (delegated to dedicated initializer)
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// 3. Hide Swagger behind Development Environment Check (P2 #7)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 4. Inject CORS and Rate Limiting into HTTP Pipeline (before authentication/authorization)
app.UseCors("FmcCorsPolicy");
app.UseRateLimiter();

app.UseCookiePolicy();

app.UseAuthentication();
app.UseAuthorization();

// Maintenance Mode Middleware — blocks non-admin API requests when active
app.Use(async (context, next) =>
{
    var cache = context.RequestServices.GetRequiredService<ICacheService>();
    var isActive = await cache.GetAsync<bool>("maintenance:mode");
    var userRoles = string.Join(",", context.User.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value));
    Console.WriteLine($"[MAINT-API-MW] path={context.Request.Path} isActive={isActive} user={context.User.Identity?.Name ?? "anon"} roles={userRoles} isSuperAdmin={context.User.IsInRole(Roles.SuperAdmin)}");
    if (isActive && !context.User.IsInRole(Roles.SuperAdmin) && !context.Request.Path.StartsWithSegments("/health") && !context.Request.Path.StartsWithSegments("/api/system/maintenance"))
    {
        context.Response.StatusCode = 503;
        context.Response.Headers["Retry-After"] = "30";
        await context.Response.WriteAsJsonAsync(new { error = "Service Unavailable", message = "System is undergoing maintenance.", retryAfter = 30 });
        return;
    }
    await next();
});

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
app.MapHealthChecks("/health");

// ─────────────────────────────────────────────────────────────
// Recurrent Background Jobs (Hangfire)
// ─────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    
    // Daily cleanup of resolved system alerts older than 90 days.
    // This ensures the database remains lean while preserving recent history.
    recurringJobManager.AddOrUpdate<NotificationJobService>(
        "system-alerts-cleanup",
        job => job.CleanupOldSystemAlertsJobAsync(),
        Cron.Daily);

    // Nightly Ledger Integrity Check (Reconciliation)
    // Runs at 2:00 AM every day to ensure balance consistency across all accounts.
    recurringJobManager.AddOrUpdate<IReconciliationService>(
        "ledger-reconciliation",
        svc => svc.ReconcileAllAccountsAsync(default),
        Cron.Daily(2, 0));
}

app.Run();

