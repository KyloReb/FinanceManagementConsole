using FMC.Application.Interfaces;
using FMC.Infrastructure.Data;
using FMC.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FMC.Shared.DTOs.User;
using System;
using System.Collections.Generic;

using Microsoft.Extensions.Configuration;


namespace FMC.Infrastructure.Authentication;

/// <summary>
/// Implementation of <see cref="IIdentityService"/> using ASP.NET Core Identity.
/// </summary>
public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtService _jwtService;
    private readonly ICacheService _cacheService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(
        UserManager<ApplicationUser> userManager, 
        IJwtService jwtService,
        ICacheService cacheService,
        IEmailService emailService,
        IConfiguration config,
        ILogger<IdentityService> logger)
    {
        _userManager = userManager;
        _jwtService = jwtService;
        _cacheService = cacheService;
        _emailService = emailService;
        _config = config;
        _logger = logger;
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
        var token = _jwtService.GenerateToken(user.Id, user.Email!, user.FirstName, user.LastName, roles, user.Organization);
        var refreshToken = _jwtService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7); // Standard 7-day expiry
        user.LastLoginAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);

        return new AuthResponseDto
        {
            Token = token,
            RefreshToken = refreshToken,
            Expiry = DateTime.UtcNow.AddMinutes(15),
            UserId = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Organization = user.Organization
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
            Organization = request.Organization,
            IsActive = true,
            EmailConfirmed = false // Must verify email
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded) return false;

        // Assign default role to every new user
        await _userManager.AddToRoleAsync(user, FMC.Shared.Auth.Roles.User);

        // Generate OTP (6-digits)
        var otp = new Random().Next(100000, 999999).ToString();
        var cacheKey = $"reg_otp_{request.Email.ToLowerInvariant()}";
        
        // Store in cache for 10 minutes
        await _cacheService.SetAsync(cacheKey, otp, TimeSpan.FromMinutes(10));

        // Retrieve logo bytes from Embedded Constant
        var logoBytes = Convert.FromBase64String(BrandingConstants.NationlinkLogoBase64);
        var attachments = new Dictionary<string, byte[]> { { "nlklogo", logoBytes } };

        _logger.LogInformation("Registration Email Logo attached via CID. Size: {Size} bytes", logoBytes.Length);

        // Build Professional HTML Email — logo attached via proper MIME CID
        var body = $@"
            <div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 12px; background-color: #ffffff;"">
                <div style=""text-align: center; padding: 20px 0;"">
                    <img src=""cid:nlklogo"" alt=""Nationlink"" width=""180"" style=""max-width: 180px; height: auto; display: block; margin: 0 auto;"" />
                    <p style=""font-size: 12px; color: #95a5a6; margin-top: 6px;"">Finance Management Console</p>
                </div>
                <div style=""background-color: #f8f9fa; padding: 30px; border-radius: 8px; text-align: center; border: 1px solid #f1f2f6;"">
                    <h2 style=""color: #2d3436; margin-top: 0; font-size: 18px; font-weight: 600;"">Activate Your Account</h2>
                    <p style=""color: #636e72; font-size: 15px; line-height: 1.5;"">Thank you for registering. Please use the verification code below to confirm your email address and activate your account.</p>
                    <div style=""margin: 24px auto; background-color: #ffffff; padding: 15px; border: 2px dashed #2980b9; border-radius: 10px; display: inline-block; max-width: 100%; box-sizing: border-box; user-select: all; word-break: break-word;"">
                        <span style=""font-size: 32px; font-weight: 800; color: #2980b9; letter-spacing: 6px; font-family: 'Courier New', monospace; user-select: all;"">{otp}</span>
                    </div>
                    <p style=""color: #95a5a6; font-size: 12px; margin-top: 8px;"">Double-click code to quickly select it</p>
                    <p style=""color: #b2bec3; font-size: 13px; margin-top: 12px;"">This code is valid for <strong>10 minutes</strong>.</p>
                </div>
                <div style=""margin-top: 30px; padding: 20px; border-top: 1px solid #eee; text-align: center;"">
                    <p style=""color: #b2bec3; font-size: 12px; line-height: 1.6;"">
                        <strong>If you did not request this account:</strong> Please ignore this email.
                    </p>
                    <p style=""color: #dfe6e9; font-size: 11px; margin-top: 12px;"">&copy; {DateTime.UtcNow.Year} Nationlink FMC Security</p>
                </div>
            </div>";

        await _emailService.SendEmailAsync(request.Email, "FMC Registration: Verification Code", body, attachments);
        
        return true;
    }

    public async Task<bool> VerifyEmailAsync(VerifyEmailRequestDto request)
    {
        var cacheKey = $"reg_otp_{request.Email.ToLowerInvariant()}";
        var storedOtp = await _cacheService.GetAsync<string>(cacheKey);

        if (string.IsNullOrEmpty(storedOtp) || storedOtp != request.Otp)
            return false;

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null) return false;

        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);

        // Consume OTP
        await _cacheService.RemoveAsync(cacheKey);

        return true;
    }

    public async Task<ForgotPasswordResponseDto?> ForgotPasswordAsync(ForgotPasswordRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Identifier) 
                ?? await _userManager.FindByNameAsync(request.Identifier);

        // Don't leak whether user exists for security
        if (user == null || string.IsNullOrEmpty(user.Email)) 
            return null;

        var otp = new Random().Next(100000, 999999).ToString();
        var cacheKey = $"fp_otp_{user.Id}";
        await _cacheService.SetAsync(cacheKey, otp, TimeSpan.FromMinutes(10));

        var logoBytes = Convert.FromBase64String(BrandingConstants.NationlinkLogoBase64);
        var attachments = new Dictionary<string, byte[]> { { "nlklogo", logoBytes } };

        _logger.LogInformation("Forgot Password Email Logo attached via CID. Size: {Size} bytes", logoBytes.Length);

        var body = $@"
            <div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 12px; background-color: #ffffff;"">
                <div style=""text-align: center; padding: 20px 0;"">
                    <img src=""cid:nlklogo"" alt=""Nationlink"" width=""180"" style=""max-width: 180px; height: auto; display: block; margin: 0 auto;"" />
                    <p style=""font-size: 12px; color: #95a5a6; margin-top: 6px;"">Finance Management Console</p>
                </div>
                <div style=""background-color: #f8f9fa; padding: 30px; border-radius: 8px; text-align: center; border: 1px solid #f1f2f6;"">
                    <h2 style=""color: #2d3436; margin-top: 0; font-size: 18px; font-weight: 600;"">Password Reset Request</h2>
                    <p style=""color: #636e72; font-size: 15px; line-height: 1.5;"">We received a request to retrieve your password. Please use the verification code below to authorize this password reset.</p>
                    <div style=""margin: 24px auto; background-color: #ffffff; padding: 15px; border: 2px dashed #2980b9; border-radius: 10px; display: inline-block; max-width: 100%; box-sizing: border-box; user-select: all; word-break: break-word;"">
                        <span style=""font-size: 32px; font-weight: 800; color: #2980b9; letter-spacing: 6px; font-family: 'Courier New', monospace; user-select: all;"">{otp}</span>
                    </div>
                    <p style=""color: #95a5a6; font-size: 12px; margin-top: 8px;"">Double-click code to quickly select it</p>
                    <p style=""color: #b2bec3; font-size: 13px; margin-top: 12px;"">This code is valid for <strong>10 minutes</strong>.</p>
                </div>
                <div style=""margin-top: 30px; padding: 20px; border-top: 1px solid #eee; text-align: center;"">
                    <p style=""color: #b2bec3; font-size: 12px; line-height: 1.6;"">
                        <strong>If you did not request this:</strong> Someone might have entered your email by mistake. You can safely ignore this email.
                    </p>
                    <p style=""color: #dfe6e9; font-size: 11px; margin-top: 12px;"">&copy; {DateTime.UtcNow.Year} Nationlink FMC Security</p>
                </div>
            </div>";

        await _emailService.SendEmailAsync(user.Email, "FMC Security Code: Password Reset", body, attachments);

        // Mask the email. Ex: j***@example.com
        var emailParts = user.Email.Split('@');
        var namePart = emailParts[0];
        var domainPart = emailParts.Length > 1 ? emailParts[1] : "";
        var maskedName = namePart.Length > 1 ? namePart.Substring(0, 1) + new string('*', namePart.Length - 1) : namePart;
        var maskedEmail = $"{maskedName}@{domainPart}";

        return new ForgotPasswordResponseDto
        {
            UserId = user.Id,
            MaskedEmail = maskedEmail
        };
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        var cacheKey = $"fp_otp_{request.UserId}";
        var storedOtp = await _cacheService.GetAsync<string>(cacheKey);

        if (string.IsNullOrEmpty(storedOtp) || storedOtp != request.Otp)
            return false;

        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null) return false;

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);

        if (result.Succeeded)
        {
            await _cacheService.RemoveAsync(cacheKey);
            return true;
        }
        return false;
    }

    public async Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow) return null;

        var roles = await _userManager.GetRolesAsync(user);
        var newToken = _jwtService.GenerateToken(user.Id, user.Email!, user.FirstName, user.LastName, roles, user.Organization);
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
            LastName = user.LastName,
            Organization = user.Organization
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
                Organization = user.Organization,
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
            Organization = user.Organization,
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
            Organization = request.Organization,
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
        user.Organization = request.Organization;
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

        // CRITICAL SECURITY GUARDRAIL: Prevent deletion of SuperAdmin accounts
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(FMC.Shared.Auth.Roles.SuperAdmin))
        {
            _logger.LogWarning("SECURITY ALERT: Attempted deletion of SuperAdmin account {Email} (ID: {Id}) blocked.", user.Email, user.Id);
            return false;
        }

        var result = await _userManager.DeleteAsync(user);
        return result.Succeeded;
    }

    public async Task<bool> InitiatePasswordChangeAsync(string userId, ChangePasswordRequestDto request)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;

        // Verify Old Password
        var isValid = await _userManager.CheckPasswordAsync(user, request.OldPassword);
        if (!isValid) return false;

        // Generate OTP (6-digits)
        var otp = new Random().Next(100000, 999999).ToString();
        var cacheKey = $"pwd_otp_{userId}";
        
        // Store in cache for 10 minutes
        await _cacheService.SetAsync(cacheKey, otp, TimeSpan.FromMinutes(10));

        // Retrieve logo bytes from Embedded Constant
        var logoBytes = Convert.FromBase64String(BrandingConstants.NationlinkLogoBase64);
        var attachments = new Dictionary<string, byte[]> { { "nlklogo", logoBytes } };
        
        _logger.LogInformation("Security Email Logo attached via CID. Size: {Size} bytes", logoBytes.Length);

        // Build Professional HTML Email — logo attached via proper MIME CID (Gmail-compatible)
        var body = $@"
            <div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 12px; background-color: #ffffff;"">
                <div style=""text-align: center; padding: 20px 0;"">
                    <img src=""cid:nlklogo"" alt=""Nationlink"" width=""180"" style=""max-width: 180px; height: auto; display: block; margin: 0 auto;"" />
                    <p style=""font-size: 12px; color: #95a5a6; margin-top: 6px;"">Finance Management Console</p>
                </div>
                <div style=""background-color: #f8f9fa; padding: 30px; border-radius: 8px; text-align: center; border: 1px solid #f1f2f6;"">
                    <h2 style=""color: #2d3436; margin-top: 0; font-size: 18px; font-weight: 600;"">Verification Required</h2>
                    <p style=""color: #636e72; font-size: 15px; line-height: 1.5;"">We received a request to update your account password. Please use the verification code below to authorize this change.</p>
                    <div style=""margin: 24px auto; background-color: #ffffff; padding: 15px; border: 2px dashed #2980b9; border-radius: 10px; display: inline-block; max-width: 100%; box-sizing: border-box; user-select: all; word-break: break-word;"">
                        <span style=""font-size: 32px; font-weight: 800; color: #2980b9; letter-spacing: 6px; font-family: 'Courier New', monospace; user-select: all;"">{otp}</span>
                    </div>
                    <p style=""color: #95a5a6; font-size: 12px; margin-top: 8px;"">Double-click code to quickly select it</p>
                    <p style=""color: #b2bec3; font-size: 13px; margin-top: 12px;"">This code is valid for <strong>10 minutes</strong>.</p>
                </div>
                <div style=""margin-top: 30px; padding: 20px; border-top: 1px solid #eee; text-align: center;"">
                    <p style=""color: #b2bec3; font-size: 12px; line-height: 1.6;"">
                        <strong>Security Reminder:</strong> If you did not request this change, please ignore this email or contact support immediately.
                    </p>
                    <p style=""color: #dfe6e9; font-size: 11px; margin-top: 12px;"">&copy; {DateTime.UtcNow.Year} Nationlink FMC Security</p>
                </div>
            </div>";

        await _emailService.SendEmailAsync(user.Email!, "FMC Security Code: Password Change", body, attachments);


        return true;
    }

    public async Task<bool> CompletePasswordChangeAsync(string userId, VerifyPasswordChangeDto request)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;

        var cacheKey = $"pwd_otp_{userId}";
        var cachedOtp = await _cacheService.GetAsync<string>(cacheKey);

        if (string.IsNullOrEmpty(cachedOtp) || cachedOtp != request.Otp)
        {
            return false;
        }

        // OTP Valid - Update Password
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);

        if (result.Succeeded)
        {
            await _cacheService.RemoveAsync(cacheKey);
            return true;
        }

        return false;
    }
}
