using Microsoft.JSInterop;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;

namespace FMC.Authentication;

/// <summary>
/// Custom AuthenticationStateProvider that manages user state based on JWT and refresh tokens from the API.
/// </summary>
public class ApiAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsStack;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PersistentComponentState _applicationState;
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    private readonly string _instanceId = Guid.NewGuid().ToString().Substring(0, 8);
    private static int _globalCount = 0;

    public ApiAuthenticationStateProvider(
        HttpClient httpClient, 
        IJSRuntime jsStack, 
        IHttpContextAccessor httpContextAccessor,
        PersistentComponentState applicationState)
    {
        _httpClient = httpClient;
        _jsStack = jsStack;
        _httpContextAccessor = httpContextAccessor;
        _applicationState = applicationState;

        int count = Interlocked.Increment(ref _globalCount);
        Console.WriteLine($"[AuthProv] Constructor #{count} called. Instance: {_instanceId}");
    }

    private string? _cachedToken;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Distinguish between Server and Circuit environments via HttpContext
        var currentContext = _httpContextAccessor.HttpContext;
        var hasValidContext = currentContext != null && currentContext.Request != null;
        var location = hasValidContext ? "Server/Pre-render" : "Circuit/WASM";
        
        Console.WriteLine($"[AuthProv] [{_instanceId}] Resolve Context: {location}");

        try
        {
            if (!string.IsNullOrEmpty(_cachedToken))
            {
                return CreateAuthStateFromToken(_cachedToken);
            }

            string? token = null;

            // 1. Priority: Cookies (Only available during Server Pre-rendering)
            if (hasValidContext)
            {
                try {
                    token = currentContext?.Request?.Cookies?["authToken"];
                    if (!string.IsNullOrEmpty(token)) {
                        Console.WriteLine($"[AuthProv] Resolved from Cookies (Length: {token.Length})");
                        _cachedToken = token;
                        return CreateAuthStateFromToken(token);
                    }
                } catch { /* Context might be partially initialized */ }
            }

            // 2. Secondary: PersistentComponentState (Bridge between Server -> Client)
            if (_applicationState.TryTakeFromJson<string>("authToken", out var persistedToken))
            {
                token = persistedToken;
                if (!string.IsNullOrEmpty(token)) {
                    Console.WriteLine($"[AuthProv] Resolved from PersistentState (Length: {token.Length})");
                    _cachedToken = token;
                    return CreateAuthStateFromToken(token);
                }
            }

            // 3. Fallback: LocalStorage (Browser persistent storage)
            try
            {
                // Note: InvokeAsync will only succeed once JS interop is active
                token = await _jsStack.InvokeAsync<string>("localStorage.getItem", "authToken");
                if (!string.IsNullOrEmpty(token)) {
                    Console.WriteLine($"[AuthProv] Resolved from LocalStorage (Length: {token.Length})");
                    _cachedToken = token;
                    return CreateAuthStateFromToken(token);
                }
            }
            catch (Exception ex)
            {
                // Expect JS errors during early hydration – ignore them
                if (hasValidContext == false && !ex.Message.Contains("prerender")) {
                    Console.WriteLine($"[AuthProv] LocalStorage lookup skipped/failed: {ex.Message}");
                }
            }

            Console.WriteLine("[AuthProv] No identity resolved. Returning Anonymous.");
            return new AuthenticationState(_anonymous);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuthProv] ERROR: {ex.Message}");
            return new AuthenticationState(_anonymous);
        }
    }

    private AuthenticationState CreateAuthStateFromToken(string token)
    {
        try 
        {
            // Sync the HttpClient header with the resolved token
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", token);

            var claims = ParseClaimsFromJwt(token);
            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);
            Console.WriteLine($"[AuthProv] Identity Resolved: {user.Identity?.Name ?? "Unknown"}");
            return new AuthenticationState(user);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuthProv] JWT Parse Error: {ex.Message}");
            return new AuthenticationState(_anonymous);
        }
    }

    public void MarkUserAsAuthenticated(string token)
    {
        _cachedToken = token;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", token);
        var authenticatedUser = new ClaimsPrincipal(new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt"));
        var authState = Task.FromResult(new AuthenticationState(authenticatedUser));
        NotifyAuthenticationStateChanged(authState);
    }

    public void MarkUserAsLoggedOut()
    {
        _cachedToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var authState = Task.FromResult(new AuthenticationState(_anonymous));
        NotifyAuthenticationStateChanged(authState);
    }

    private IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
        return keyValuePairs!.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString()!));
    }

    private byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
