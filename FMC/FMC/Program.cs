using FMC.Client.Pages;
using FMC.Components;
using MudBlazor.Services;
using FMC.Services;
using FMC.Services.Api;
using FMC.Authentication;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
#region API & Authentication Configuration
builder.Services.AddHttpClient("FMC.Api", client => 
{
    client.BaseAddress = new Uri(builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7026/");
}).AddHttpMessageHandler<AuthenticationHeaderHandler>();

builder.Services.AddTransient<AuthenticationHeaderHandler>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("FMC.Api"));

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();
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
                var payload = parts[1];
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
