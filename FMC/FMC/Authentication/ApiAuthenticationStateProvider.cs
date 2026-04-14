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
    private readonly IJSRuntime _jsStack;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PersistentComponentState _applicationState;
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    private readonly string _instanceId = Guid.NewGuid().ToString().Substring(0, 8);
    private static int _globalCount = 0;

    public ApiAuthenticationStateProvider(
        IJSRuntime jsStack, 
        IHttpContextAccessor httpContextAccessor,
        PersistentComponentState applicationState)
    {
        _jsStack = jsStack;
        _httpContextAccessor = httpContextAccessor;
        _applicationState = applicationState;

        int count = Interlocked.Increment(ref _globalCount);
        Console.WriteLine($"[AuthProv] Constructor #{count} called. Instance: {_instanceId}");
    }

    private string? _cachedToken;
    public string? CurrentToken => _cachedToken;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var currentContext = _httpContextAccessor.HttpContext;
        var hasValidContext = currentContext != null && currentContext.Request != null;
        var location = hasValidContext ? "Server/Pre-render" : "Circuit/WASM";
        
        Console.WriteLine($"[AuthProv] [{_instanceId}] Resolve Context: {location}");

        try
        {
            // Use cached token only if it is still valid
            if (!string.IsNullOrEmpty(_cachedToken))
            {
                if (IsTokenExpired(_cachedToken))
                {
                    Console.WriteLine($"[AuthProv] [{_instanceId}] Cached token is EXPIRED. Clearing.");
                    _cachedToken = null;
                }
                else
                {
                    return CreateAuthStateFromToken(_cachedToken);
                }
            }

            string? token = null;

            // 1. Priority: Cookies (Only available during Server Pre-rendering)
            if (hasValidContext)
            {
                try {
                    token = currentContext?.Request?.Cookies?["authToken"];
                    if (!string.IsNullOrEmpty(token))
                    {
                        if (IsTokenExpired(token))
                        {
                            Console.WriteLine($"[AuthProv] Cookie token is EXPIRED. Ignoring.");
                            token = null;
                        }
                        else
                        {
                            Console.WriteLine($"[AuthProv] Resolved from Cookies (Length: {token.Length})");
                            _cachedToken = token;
                            return CreateAuthStateFromToken(token);
                        }
                    }
                } catch { /* Context might be partially initialized */ }
            }

            // 2. Secondary: PersistentComponentState (Bridge between Server -> Client)
            if (_applicationState.TryTakeFromJson<string>("authToken", out var persistedToken))
            {
                token = persistedToken;
                if (!string.IsNullOrEmpty(token))
                {
                    if (IsTokenExpired(token))
                    {
                        Console.WriteLine($"[AuthProv] PersistentState token is EXPIRED. Ignoring.");
                        token = null;
                    }
                    else
                    {
                        Console.WriteLine($"[AuthProv] Resolved from PersistentState (Length: {token.Length})");
                        _cachedToken = token;
                        return CreateAuthStateFromToken(token);
                    }
                }
            }

            // 3. Fallback: LocalStorage (Browser persistent storage)
            try
            {
                token = await _jsStack.InvokeAsync<string>("localStorage.getItem", "authToken");
                if (!string.IsNullOrEmpty(token))
                {
                    if (IsTokenExpired(token))
                    {
                        Console.WriteLine($"[AuthProv] LocalStorage token is EXPIRED. Clearing.");
                        try { await _jsStack.InvokeVoidAsync("localStorage.removeItem", "authToken"); } catch { }
                        token = null;
                    }
                    else
                    {
                        Console.WriteLine($"[AuthProv] Resolved from LocalStorage (Length: {token.Length})");
                        _cachedToken = token;
                        return CreateAuthStateFromToken(token);
                    }
                }
            }
            catch (Exception ex)
            {
                if (hasValidContext == false && !ex.Message.Contains("prerender"))
                    Console.WriteLine($"[AuthProv] LocalStorage lookup skipped/failed: {ex.Message}");
            }

            Console.WriteLine("[AuthProv] No valid identity resolved. Returning Anonymous.");
            return new AuthenticationState(_anonymous);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuthProv] ERROR: {ex.Message}");
            return new AuthenticationState(_anonymous);
        }
    }

    /// <summary>Returns true if the JWT token is expired (with a 10-second buffer).</summary>
    private static bool IsTokenExpired(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return true;
        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2) return true;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            while (payload.Length % 4 != 0) payload += "=";
            var jsonBytes = Convert.FromBase64String(payload);
            using var doc = System.Text.Json.JsonDocument.Parse(jsonBytes);
            if (doc.RootElement.TryGetProperty("exp", out var expProp))
            {
                var expTime = DateTimeOffset.FromUnixTimeSeconds(expProp.GetInt64());
                var isExpired = expTime <= DateTimeOffset.UtcNow.AddSeconds(10);
                if (isExpired) Console.WriteLine($"[AuthProv] Token is EXPIRED (Exp: {expTime:u}, Now: {DateTimeOffset.UtcNow:u})");
                return isExpired;
            }
            // If exp claim is missing entirely, we treat it as potentially permanent but suspicious in this system
            return false; 
        }
        catch (Exception ex)
        { 
            Console.WriteLine($"[AuthProv] Token expiration check FAILED: {ex.Message}");
            return true; // Treat unparseable tokens as expired for security
        }
    }


    private AuthenticationState CreateAuthStateFromToken(string token)
    {
        try 
        {
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
        var authenticatedUser = new ClaimsPrincipal(new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt"));
        var authState = Task.FromResult(new AuthenticationState(authenticatedUser));
        NotifyAuthenticationStateChanged(authState);
    }

    public void MarkUserAsLoggedOut()
    {
        _cachedToken = null;
        var authState = Task.FromResult(new AuthenticationState(_anonymous));
        NotifyAuthenticationStateChanged(authState);
    }

    private IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var claims = new List<Claim>();
        var payload = jwt.Split('.')[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        
        using var jsonDoc = JsonDocument.Parse(jsonBytes);
        foreach (var prop in jsonDoc.RootElement.EnumerateObject())
        {
            var key = prop.Name;
            var value = prop.Value;

            // Normalize key if it's a short-name for standard Identity claims
            if (key == "role") key = ClaimTypes.Role;
            if (key == "unique_name" || key == "name") key = ClaimTypes.Name;
            if (key == "sub") key = ClaimTypes.NameIdentifier;
            if (key == "email") key = ClaimTypes.Email;

            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in value.EnumerateArray())
                {
                    claims.Add(new Claim(key, element.ToString()));
                }
            }
            else
            {
                claims.Add(new Claim(key, value.ToString()));
            }
        }
        return claims;
    }

    private byte[] ParseBase64WithoutPadding(string base64)
    {
        base64 = base64.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
