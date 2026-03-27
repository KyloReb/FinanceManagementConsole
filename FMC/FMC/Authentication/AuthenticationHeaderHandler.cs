using System.Net.Http.Headers;
using Microsoft.JSInterop;

namespace FMC.Authentication;

/// <summary>
/// A delegating handler that automatically retrieves the JWT token from browser local storage 
/// and attaches it to the Authorization header for all outgoing API requests.
/// </summary>
public class AuthenticationHeaderHandler : DelegatingHandler
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthenticationHeaderHandler(IJSRuntime jsRuntime, IHttpContextAccessor httpContextAccessor)
    {
        _jsRuntime = jsRuntime;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? token = null;

        // 1. Priority: Try HttpContext Cookies (Only available during Server-side execution)
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                token = httpContext.Request.Cookies["authToken"];
            }
        }
        catch { /* Context might not be available - skip */ }

        // 2. Secondary: Fallback to LocalStorage (Only available during Interactive Client-side execution)
        if (string.IsNullOrEmpty(token))
        {
            try
            {
                // Only attempt JS interop if we are likely in a circuit or browser environment.
                token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", new object[] { "authToken" }, cancellationToken);
            }
            catch (NotSupportedException) { /* Prerendering - ignore */ }
            catch (JSDisconnectedException) { /* Disconnected - ignore */ }
            catch (Exception) { /* Other JS failures - ignore */ }
        }

        // Only attach the token if one is found and the request doesn't already have one 
        // (to prevent overwriting manual overrides from AuthService during login transitions)
        if (request.Headers.Authorization == null && !string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Silent cancellation - return a 499 or similar if needed, 
            // but for Blazor, just propagating is often fine if caught upstream.
            // Here we re-throw to allow upstream catch blocks to recognize the cancellation.
            throw;
        }
    }
}
