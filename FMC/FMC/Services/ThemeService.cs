using Microsoft.JSInterop;
using Microsoft.AspNetCore.Http;

namespace FMC.Services;

/// <summary>
/// Service responsible for managing the application's visual theme (Dark/Light mode).
/// Persists user preferences using Cookies for server-side awareness and LocalStorage for client-side fallback.
/// </summary>
public class ThemeService
{
    private readonly IJSRuntime _js;
    private bool _isDarkMode = false; // Default to Light Mode to avoid dark flash for light theme users

    /// <summary>
    /// Gets or sets a value indicating whether the application is in Dark Mode.
    /// Automatically persists the value to the user's browser.
    /// </summary>
    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (_isDarkMode != value)
            {
                _isDarkMode = value;
                NotifyStateChanged();
                _ = SavePreference();
                _ = UpdateHtmlClass();
            }
        }
    }

    /// <summary>
    /// Event triggered when the theme state changes.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeService"/> class.
    /// Reads the initial theme preference from cookies to prevent "Flash of Unstyled Content" (FOUC) during pre-rendering.
    /// </summary>
    /// <param name="js">The JS runtime for client-side interactions.</param>
    /// <param name="httpContextAccessor">Accessor to the current HTTP context for reading cookies.</param>
    public ThemeService(IJSRuntime js, IHttpContextAccessor httpContextAccessor)
    {
        _js = js;
        var cookie = httpContextAccessor.HttpContext?.Request.Cookies["theme_preference"];
        if (cookie != null)
        {
            _isDarkMode = cookie == "dark";
        }
    }

    /// <summary>
    /// Synchronizes the theme state with the client-side storage.
    /// Useful for ensuring alignment after the interactive circuit is established.
    /// </summary>
    public async Task InitializeAsync()
    {
        try 
        {
            var preference = await _js.InvokeAsync<string>("localStorage.getItem", "theme_preference");
            if (preference != null)
            {
                var isDark = preference == "dark";
                if (_isDarkMode != isDark)
                {
                    _isDarkMode = isDark;
                    NotifyStateChanged();
                }
                await UpdateHtmlClass(); // Ensure initial state is sync'd
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or JSDisconnectedException) 
        { 
            /* Expected during circuit disposal or navigation interruptions */ 
        }
    }

    /// <summary>
    /// Persists the current theme preference to the user's browser.
    /// Sets both a cookie (for the server) and local storage (for the client).
    /// </summary>
    private async Task SavePreference()
    {
        try
        {
            var value = _isDarkMode ? "dark" : "light";
            await _js.InvokeVoidAsync("localStorage.setItem", "theme_preference", value);
            // Use a JS function to set the cookie securely and reliably
            await _js.InvokeVoidAsync("cookieHelper.setCookie", "theme_preference", value, 365, true, "Lax");
        }
        catch (Exception ex) when (ex is OperationCanceledException or JSDisconnectedException) { }
    }

    /// <summary>
    /// Dynamically synchronizes the 'dark' class on the <html> element to ensure consistent background rendering.
    /// </summary>
    private async Task UpdateHtmlClass()
    {
        try
        {
            await _js.InvokeVoidAsync("eval", _isDarkMode 
                ? "document.documentElement.classList.add('dark'); document.documentElement.style.backgroundColor = '#11111b';" 
                : "document.documentElement.classList.remove('dark'); document.documentElement.style.backgroundColor = '#ffffff';");
        }
        catch (Exception ex) when (ex is OperationCanceledException or JSDisconnectedException) { }
    }

    /// <summary>
    /// Notifies listeners that the service state has changed.
    /// </summary>
    private void NotifyStateChanged() => OnChange?.Invoke();
}
