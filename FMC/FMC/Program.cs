using FMC.Client.Pages;
using FMC.Components;
using MudBlazor.Services;
using FMC.Services;
using FMC.Services.Api;
using FMC.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;

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
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/login";
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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
    options.Secure = CookieSecurePolicy.Always;
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
});
#endregion

#region Custom Application Services
builder.Services.AddScoped<ApiFinanceService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<OrganizationApiService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ThemeService>();
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
app.UseHttpsRedirection();
app.UseCookiePolicy();
app.UseStaticFiles();

// Custom Middleware to bridge the authToken cookie to the HttpContext.User
// and prevent browser history caching of sensitive views
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
            var parts = token.Split('.');
            if (parts.Length > 1)
            {
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                while (payload.Length % 4 != 0) payload += "=";
                var jsonBytes = Convert.FromBase64String(payload);
                var claimsDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
                if (claimsDict != null)
                {
                    var claims = claimsDict.Select(kvp => new System.Security.Claims.Claim(kvp.Key, kvp.Value.ToString()!));
                    var identity = new System.Security.Claims.ClaimsIdentity(claims, "CookieBridge");
                    context.User = new System.Security.Claims.ClaimsPrincipal(identity);
                }
            }
        }
        catch { }
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(FMC.Client._Imports).Assembly);

#endregion

app.Run();
