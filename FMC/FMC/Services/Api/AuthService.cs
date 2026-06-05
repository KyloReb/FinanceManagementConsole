using Microsoft.JSInterop;
using System.Net;
using System.Net.Http.Json;
using FMC.Authentication;
using FMC.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace FMC.Services.Api;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly IJSRuntime _js;

    public Task<AuthenticationState> GetAuthenticationStateAsync() => _authStateProvider.GetAuthenticationStateAsync();

    public AuthService(HttpClient httpClient, AuthenticationStateProvider authStateProvider, IJSRuntime js)
    {
        _httpClient = httpClient;
        _authStateProvider = authStateProvider;
        _js = js;
    }

    public async Task<(AuthResponseDto? Result, HttpStatusCode StatusCode)> Login(LoginRequestDto loginRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginRequest);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return (null, HttpStatusCode.TooManyRequests);
        if (!response.IsSuccessStatusCode)
            return (null, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        if (result != null)
        {
            await _js.InvokeVoidAsync("secureCookieHelper.setSecureCookie", result.Token, loginRequest.RememberMe);
            ((ApiAuthenticationStateProvider)_authStateProvider).MarkUserAsAuthenticated(result.Token);
        }

        return (result, HttpStatusCode.OK);
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
        await _js.InvokeVoidAsync("secureCookieHelper.deleteSecureCookie");
        ((ApiAuthenticationStateProvider)_authStateProvider).MarkUserAsLoggedOut();
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

    public async Task<FMC.Shared.DTOs.User.UserDto?> GetCurrentUserAsync()
    {
        var response = await _httpClient.GetAsync("api/users/me");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<FMC.Shared.DTOs.User.UserDto>();
    }
}
