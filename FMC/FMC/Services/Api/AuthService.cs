using Microsoft.JSInterop;
using System.Net.Http.Json;
using FMC.Authentication;
using FMC.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace FMC.Services.Api;

/// <summary>
/// Client-side service for handling authentication requests to the API.
/// </summary>
public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly IJSRuntime _js;

    public AuthService(HttpClient httpClient, AuthenticationStateProvider authStateProvider, IJSRuntime js)
    {
        _httpClient = httpClient;
        _authStateProvider = authStateProvider;
        _js = js;
    }

    public async Task<AuthResponseDto?> Login(LoginRequestDto loginRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginRequest);
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        if (result != null)
        {
            await _js.InvokeVoidAsync("localStorage.setItem", "authToken", result.Token);
            
            // Set a cookie for the server to read during pre-rendering
            var cookieExpiryDays = loginRequest.RememberMe ? 30 : 1;
            await _js.InvokeVoidAsync("cookieHelper.setCookie", "authToken", result.Token, cookieExpiryDays, true, "Lax");

            ((ApiAuthenticationStateProvider)_authStateProvider).MarkUserAsAuthenticated(result.Token);
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("bearer", result.Token);
        }

        return result;
    }

    public async Task<bool> Register(RegisterRequestDto registerRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/register", registerRequest);
        return response.IsSuccessStatusCode;
    }

    public async Task Logout(string userId)
    {
        await _httpClient.PostAsync($"api/auth/logout?userId={userId}", null);
        await _js.InvokeVoidAsync("localStorage.removeItem", "authToken");
        await _js.InvokeVoidAsync("cookieHelper.deleteCookie", "authToken");
        ((ApiAuthenticationStateProvider)_authStateProvider).MarkUserAsLoggedOut();
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }
}
