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
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.Token);
        }

        return result;
    }

    public async Task<bool> Register(RegisterRequestDto registerRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/register", registerRequest);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> VerifyEmail(VerifyEmailRequestDto request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/verify-email", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<ForgotPasswordResponseDto?> ForgotPassword(ForgotPasswordRequestDto request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/forgot-password", request);
        if (!response.IsSuccessStatusCode) return null;
        
        var content = await response.Content.ReadFromJsonAsync<ForgotPasswordResponseDto>();
        return content;
    }

    public async Task<bool> ResetPassword(ResetPasswordRequestDto request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/reset-password", request);
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

    public async Task<bool> InitiatePasswordChange(ChangePasswordRequestDto request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/change-password/initiate", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> CompletePasswordChange(VerifyPasswordChangeDto request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/change-password/complete", request);
        return response.IsSuccessStatusCode;
    }
}
