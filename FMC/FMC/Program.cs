using FMC.Client.Pages;
using FMC.Components;
using MudBlazor.Services;
using FMC.Services;
using FMC.Services.Api;
using FMC.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
#region Culture Configuration
var cultureInfo = new System.Globalization.CultureInfo("en-PH");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
#endregion

#region API & Authentication Configuration
// Build the HttpClient + AuthenticationHeaderHandler directly within the Blazor circuit scope.
// IHttpClientFactory resolves handler pipelines through an internal scope that is SEPARATE
// from the circuit scope, causing ApiAuthenticationStateProvider to have different instances
// across AuthService and the handler — breaking MarkUserAsAuthenticated. This fixes it.
builder.Services.AddScoped<HttpClient>(sp =>
{
    var stateProvider   = sp.GetRequiredService<ApiAuthenticationStateProvider>();
    var logger          = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AuthenticationHeaderHandler>>();
    var config          = sp.GetRequiredService<IConfiguration>();

    // Safely retrieve accessor, avoiding DI cycles during transient instantiation
    var httpCtxAccessor = sp.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>() ?? new Microsoft.AspNetCore.Http.HttpContextAccessor();

    var authHandler = new AuthenticationHeaderHandler(stateProvider, httpCtxAccessor, logger)
    {
        InnerHandler = new HttpClientHandler()
    };

    var baseUrl = config["ApiSettings:BaseUrl"] ?? "https://localhost:7026/";
    return new HttpClient(authHandler) { BaseAddress = new Uri(baseUrl) };
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<ApiAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ApiAuthenticationStateProvider>());
builder.Services.AddScoped<AuthService>();
builder.Services.AddAuthorizationCore();

// Added minimal local authentication to satisfy middleware requirements
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "FMC_Local_Scheme";
}).AddCookie("FMC_Local_Scheme", options =>
{
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/login";
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
});
#endregion

#region Blazor & Web UI Configuration
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddMudServices();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = CookieSecurePolicy.SameAsRequest;
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
});
#endregion

#region Custom Application Services
builder.Services.AddScoped<ApiFinanceService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<OrganizationApiService>();
builder.Services.AddScoped<BulkUploadStateService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<GlobalAlertService>();
builder.Services.AddScoped<SecurityStateService>();
builder.Services.AddScoped<FMC.Application.Interfaces.ICurrentUserService, BlazorCurrentUserService>();
builder.Services.AddSingleton<MaintenancePoller>();
#endregion

var app = builder.Build();

// Configure the HTTP request pipeline.
#region HTTP Request Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCookiePolicy();
app.UseStaticFiles();

// 1. Secure HTTP Headers Middleware (CSP, Clickjacking, MIME-Sniffing)
if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Content-Security-Policy", 
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net; " +
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
            "font-src 'self' https://fonts.gstatic.com; " +
            "img-src 'self' data:; " +
            "connect-src 'self' http://localhost:7026 https://localhost:7026 http://localhost:7027 http://172.31.0.152:7027 ws://localhost:7031 wss://localhost:7031;");
        await next();
    });
}

// Custom Middleware to bridge the authToken cookie to the HttpContext.User
// and prevent browser history caching of sensitive views (with cryptographically secure JWT signature verification)
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0");
    context.Response.Headers.Append("Pragma", "no-cache");
    context.Response.Headers.Append("Expires", "0");

    var token = context.Request.Cookies["authToken"];
    if (!string.IsNullOrEmpty(token) && (context.User.Identity == null || !context.User.Identity.IsAuthenticated))
    {
        try
        {
            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            var secret = config["JwtSettings:Secret"] ?? throw new InvalidOperationException("JWT Secret is missing.");
            var issuer = config["JwtSettings:Issuer"] ?? "FMC.Api";
            var audience = config["JwtSettings:Audience"] ?? "FMC.UI";

            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            
            var claims = principal.Claims.ToList();
            var normalizedClaims = new List<System.Security.Claims.Claim>();
            foreach (var claim in claims)
            {
                var key = claim.Type;
                if (key == "role" || key == System.Security.Claims.ClaimTypes.Role) normalizedClaims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, claim.Value));
                else if (key == "unique_name" || key == "name" || key == System.Security.Claims.ClaimTypes.Name) normalizedClaims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, claim.Value));
                else if (key == "sub" || key == System.Security.Claims.ClaimTypes.NameIdentifier) normalizedClaims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, claim.Value));
                else if (key == "email" || key == System.Security.Claims.ClaimTypes.Email) normalizedClaims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, claim.Value));
                else normalizedClaims.Add(new System.Security.Claims.Claim(key, claim.Value));
            }

            var identity = new System.Security.Claims.ClaimsIdentity(normalizedClaims, "CookieBridge");
            context.User = new System.Security.Claims.ClaimsPrincipal(identity);
        }
        catch { /* Invalid signature, expired token, etc. - fall back to anonymous context safely */ }
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

// Maintenance Mode Middleware — blocks non-admin requests when maintenance is active
app.Use(async (context, next) =>
{
    // Always allow login, forgot-password, static assets, and Blazor frameworks through
    var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
    if (path.StartsWith("/login") || path.StartsWith("/forgot-password") || path.StartsWith("/verify-email") ||
        path.StartsWith("/not-found") || path.StartsWith("/_framework") || path.StartsWith("/_content") ||
        path.StartsWith("/_blazor") || path.StartsWith("/css/") || path.StartsWith("/js/") ||
        path.StartsWith("/api/local-auth/") ||
        path.StartsWith("/lib/") || path.EndsWith(".png") || path.EndsWith(".ico") ||
        path.EndsWith(".svg") || path.EndsWith(".css") || path.EndsWith(".js"))
    {
        await next();
        return;
    }

    if (!FMC.Services.MaintenanceState.IsActive)
    {
        // Check grace period (maintenance scheduled but not yet active)
        if (FMC.Services.MaintenanceState.ScheduledAt.HasValue && FMC.Services.MaintenanceState.GraceMinutes > 0)
        {
            var graceStart = FMC.Services.MaintenanceState.ScheduledAt.Value.AddMinutes(-FMC.Services.MaintenanceState.GraceMinutes);
            var now = DateTime.UtcNow;
            if (now >= graceStart && now < FMC.Services.MaintenanceState.ScheduledAt.Value && !context.User.IsInRole(FMC.Shared.Auth.Roles.SuperAdmin))
            {
                var remaining = (FMC.Services.MaintenanceState.ScheduledAt.Value - now).TotalSeconds;
                var msg = FMC.Services.MaintenanceState.Message;
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(MaintenancePage.GraceHtml((int)remaining, msg));
                return;
            }
        }
        await next();
        return;
    }
    if (!context.User.IsInRole(FMC.Shared.Auth.Roles.SuperAdmin))
    {
        var msg = FMC.Services.MaintenanceState.Message;
        var modeType = FMC.Services.MaintenanceState.ModeType;

        // Read-only mode: allow GET, block POST/PUT/DELETE
        if (modeType == "readonly")
        {
            var method = context.Request.Method.ToUpperInvariant();
            if (method == "GET" || method == "HEAD" || method == "OPTIONS")
            {
                await next();
                return;
            }
        }

        context.Response.StatusCode = 503;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(MaintenancePage.FullLockHtml(msg));
        return;
    }
    await next();
});

app.UseAntiforgery();

app.MapStaticAssets();

// Minimal API endpoints for managing the HttpOnly secure token cookies securely from client-side requests
app.MapPost("/api/local-auth/set-token", async (HttpContext httpContext, TokenSetRequest request) =>
{
    var cookieOptions = new CookieOptions
    {
        HttpOnly = true,
        Secure = httpContext.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Expires = request.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddDays(1)
    };
    httpContext.Response.Cookies.Append("authToken", request.Token, cookieOptions);
    return Results.Ok();
});

app.MapPost("/api/local-auth/clear-token", (HttpContext httpContext) =>
{
    httpContext.Response.Cookies.Delete("authToken");
    return Results.Ok();
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(FMC.Client._Imports).Assembly);

// Start background maintenance state polling (syncs with API every 5s)
app.Services.GetRequiredService<MaintenancePoller>().Start();

#endregion

app.Run();

public record TokenSetRequest(string Token, bool RememberMe);
