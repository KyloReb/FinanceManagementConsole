using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using FMC.Models;
using FMC.Data;
using Microsoft.EntityFrameworkCore;

namespace FMC.Controllers;

[Route("api/[controller]")]
[ApiController]
/// <summary>
/// Provides secure REST endpoints for managing HTTP cookie-based authentication sessions.
/// </summary>
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    /// <summary>
    /// Processes a native HTML form submission to establish a secure, encrypted authentication cookie.
    /// Redirects the user immediately following success or failure.
    /// </summary>
    /// <param name="username">The provided registered username or email address.</param>
    /// <param name="password">The plaintext password.</param>
    /// <param name="returnUrl">The relative URL path to redirect the user to upon success.</param>
    /// <param name="rememberMe">If true, issues a persistent session cookie lasting across browser restarts.</param>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password, [FromForm] string? returnUrl, [FromForm] bool rememberMe = false)
    {
        var user = username.Contains("@") 
            ? await _userManager.Users.FirstOrDefaultAsync(u => u.Email == username) 
            : await _userManager.FindByNameAsync(username);

        if (user != null)
        {
            var result = await _signInManager.PasswordSignInAsync(user.UserName, password, isPersistent: rememberMe, lockoutOnFailure: true);
            
            if (result.Succeeded)
            {
                return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
            }
        }
        
        // Fallback if direct POST is ever triggered with invalid credentials
        return LocalRedirect($"/login?error=true&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
    }

    /// <summary>
    /// Destroys the current user's authentication cookie, effectively ending their session.
    /// Automatically redirects the user back to the login page.
    /// </summary>
    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return LocalRedirect("/login");
    }
}
