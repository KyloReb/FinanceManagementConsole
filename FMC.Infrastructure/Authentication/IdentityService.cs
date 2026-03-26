using FMC.Application.Interfaces;
using FMC.Infrastructure.Data;
using FMC.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FMC.Shared.DTOs.User;

namespace FMC.Infrastructure.Authentication;

/// <summary>
/// Implementation of <see cref="IIdentityService"/> using ASP.NET Core Identity.
/// </summary>
public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtService _jwtService;

    public IdentityService(UserManager<ApplicationUser> userManager, IJwtService jwtService)
    {
        _userManager = userManager;
        _jwtService = jwtService;
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto request)
    {
        // Try to find user by Email first
        var user = await _userManager.FindByEmailAsync(request.Identifier);
        
        // Fallback to Username if not found by email
        if (user == null)
        {
            user = await _userManager.FindByNameAsync(request.Identifier);
        }

        if (user == null || !user.IsActive) return null;

        var result = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!result) return null;

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtService.GenerateToken(user.Id, user.Email!, user.FirstName, user.LastName, roles);
        var refreshToken = _jwtService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7); // Standard 7-day expiry
        user.LastLoginAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);

        return new AuthResponseDto
        {
            Token = token,
            RefreshToken = refreshToken,
            Expiry = DateTime.UtcNow.AddMinutes(15), // Standard 15-min JWT
            UserId = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName
        };
    }

    public async Task<bool> RegisterAsync(RegisterRequestDto request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null) return false;

        var user = new ApplicationUser
        {
            UserName = request.Username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = true,
            EmailConfirmed = true // Default for demo/internal console
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded) return false;

        // Assign default role to every new user
        await _userManager.AddToRoleAsync(user, FMC.Shared.Auth.Roles.User);
        
        return true;
    }

    public async Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow) return null;

        var roles = await _userManager.GetRolesAsync(user);
        var newToken = _jwtService.GenerateToken(user.Id, user.Email!, user.FirstName, user.LastName, roles);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        await _userManager.UpdateAsync(user);

        return new AuthResponseDto
        {
            Token = newToken,
            RefreshToken = newRefreshToken,
            Expiry = DateTime.UtcNow.AddMinutes(15),
            UserId = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName
        };
    }

    public async Task<bool> LogoutAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        await _userManager.UpdateAsync(user);
        return true;
    }

    public async Task<List<UserDto>> GetAllUsersAsync()
    {
        var users = await _userManager.Users.ToListAsync();
        var userDtos = new List<UserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userDtos.Add(new UserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                Roles = roles.ToList()
            });
        }

        return userDtos;
    }

    public async Task<UserDto?> GetUserByIdAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return null;

        var roles = await _userManager.GetRolesAsync(user);
        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsActive = user.IsActive,
            Roles = roles.ToList()
        };
    }

    public async Task<bool> CreateUserAsync(CreateUserDto request)
    {
        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded) return false;

        if (request.Roles.Any())
        {
            await _userManager.AddToRolesAsync(user, request.Roles);
        }
        else
        {
            await _userManager.AddToRoleAsync(user, FMC.Shared.Auth.Roles.User);
        }

        return true;
    }

    public async Task<bool> UpdateUserAsync(UpdateUserDto request)
    {
        var user = await _userManager.FindByIdAsync(request.Id);
        if (user == null) return false;

        user.Email = request.Email;
        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.IsActive = request.IsActive;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) return false;

        // Sync Roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRolesAsync(user, request.Roles);

        // Update Password if provided
        if (!string.IsNullOrEmpty(request.Password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _userManager.ResetPasswordAsync(user, token, request.Password);
        }

        return true;
    }

    public async Task<bool> DeleteUserAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return false;

        var result = await _userManager.DeleteAsync(user);
        return result.Succeeded;
    }
}
