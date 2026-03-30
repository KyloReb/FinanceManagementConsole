using System.Net.Http.Headers;
using Microsoft.JSInterop;
using System.Text.Json;

namespace FMC.Authentication;

/// <summary>
/// A delegating handler that automatically retrieves the JWT token from browser local storage 
/// and attaches it to the Authorization header for all outgoing API requests.
/// </summary>
public class AuthenticationHeaderHandler : DelegatingHandler
{
    private readonly Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider _authStateProvider;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;
    private readonly Microsoft.Extensions.Logging.ILogger<AuthenticationHeaderHandler> _logger;

    public AuthenticationHeaderHandler(
        Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider authStateProvider,
        Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
        Microsoft.Extensions.Logging.ILogger<AuthenticationHeaderHandler> logger)
    {
        _authStateProvider = authStateProvider;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? token = null;

        // 1. Skip token attachment for Auth endpoints (Login, Register, etc.)
        // These must be anonymous and sending an expired token can trigger 401s in middleware
        if (request.RequestUri?.PathAndQuery.Contains("/api/auth/", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Even if anonymous, we still want to forward the User-Agent for audit logs
            ForwardUserAgent(request);
            return await base.SendAsync(request, cancellationToken);
        }

        ForwardUserAgent(request);

        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            if (authState.User.Identity?.IsAuthenticated == true)
            {
                if (_authStateProvider is ApiAuthenticationStateProvider apiStateProv)
                {
                    token = apiStateProv.CurrentToken; 
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("[AuthHandler] Could not resolve identity from provider: {Msg}", ex.Message);
        }

        // 2. Validate token existence and freshness
        bool isTokenValid = !string.IsNullOrEmpty(token);
        
        // Optional: Proactive Expiry Check (Minimal parsing of UTC 'exp' claim)
        if (isTokenValid)
        {
            try 
            {
                var parts = token!.Split('.');
                if (parts.Length > 1)
                {
                    var payload = parts[1]
                        .Replace('-', '+')
                        .Replace('_', '/');
                    while (payload.Length % 4 != 0) payload += "=";
                    var jsonBytes = Convert.FromBase64String(payload);
                    using var doc = JsonDocument.Parse(jsonBytes);
                    if (doc.RootElement.TryGetProperty("exp", out var expProp))
                    {
                        var expUnix = expProp.GetInt64();
                        var expTime = DateTimeOffset.FromUnixTimeSeconds(expUnix);
                        if (expTime <= DateTimeOffset.UtcNow.AddSeconds(5)) // 5s buffer
                        {
                            _logger.LogWarning("[AuthHandler] Token for {Url} is EXPIRED. Skipping attachment.", request.RequestUri);
                            isTokenValid = false;
                        }
                    }
                }
            }
            catch { /* Ignore parse errors - let the API handle validation if unsure */ }
        }

        // 3. Attach the token if one is found and valid
        if (request.Headers.Authorization == null && isTokenValid)
        {
            var tokenSnippet = token!.Length > 15 ? token.Substring(0, 10) + "..." : token;
            _logger.LogInformation("[AuthHandler] Attaching Bearer token (Len: {Len}) to request {Url}", token.Length, request.RequestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else if (request.Headers.Authorization != null)
        {
             _logger.LogInformation("[AuthHandler] Request {Url} already has Authorization header. Skipping attachment.", request.RequestUri);
        }
        else 
        {
             _logger.LogWarning("[AuthHandler] NO VALID TOKEN FOUND for request {Url}", request.RequestUri);
        }

        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogError("[AuthHandler] API REJECTED REQUEST (401) for {Url} despite having token: {HasToken}", request.RequestUri, !string.IsNullOrEmpty(token));
            }
            return response;
        }
        catch (OperationCanceledException)
        {
            // Silent cancellation - return a 499 or similar if needed, 
            // but for Blazor, just propagating is often fine if caught upstream.
            // Here we re-throw to allow upstream catch blocks to recognize the cancellation.
            throw;
        }
    }

    private void ForwardUserAgent(HttpRequestMessage request)
    {
        try
        {
            // In Blazor Server, retrieve the original browser's User-Agent via HttpContext
            var context = _httpContextAccessor.HttpContext;
            if (context != null)
            {
                var userAgent = context.Request.Headers.UserAgent.ToString();
                if (!string.IsNullOrEmpty(userAgent) && !request.Headers.UserAgent.Any())
                {
                    request.Headers.UserAgent.ParseAdd(userAgent);
                }
            }
        }
        catch { /* Diagnostic: Forwarding failed, fallback to default HttpClient behavior */ }
    }
}
