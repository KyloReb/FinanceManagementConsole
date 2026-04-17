using FMC.Shared.DTOs.Auth;
using FMC.Shared.DTOs.User;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FMC.Application.Interfaces;

/// <summary>
/// Proved a high-level abstraction for core Identity operations, decoupling the application from ASP.NET Identity details.
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Authenticates a user based on their email and password.
    /// </summary>
    /// <returns>An AuthResponseDto if successful; otherwise null.</returns>
    Task<AuthResponseDto?> LoginAsync(LoginRequestDto request);

    // Public Registration and Email Verification methods removed to enforce administrative-only user provisioning.

    /// <summary>
    /// Initiates a forgot password flow, generating an OTP and sending an email.
    /// Returns the user's masked email address and ID.
    /// </summary>
    Task<ForgotPasswordResponseDto?> ForgotPasswordAsync(ForgotPasswordRequestDto request);

    /// <summary>
    /// Verifies the forgot password OTP and resets the user's password.
    /// </summary>
    Task<bool> ResetPasswordAsync(ResetPasswordRequestDto request);

    /// <summary>
    /// Refreshes an expired access token using a valid refresh token.
    /// </summary>
    Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Revokes the current user's refresh token to invalidate their session.
    /// </summary>
    Task<bool> LogoutAsync(string userId);

    /// <summary>
    /// Retrieves a list of all users in the system.
    /// </summary>
    Task<List<UserDto>> GetAllUsersAsync();

    /// <summary>
    /// Retrieves a list of users belonging to a specific organization.
    /// </summary>
    Task<List<UserDto>> GetUsersByOrganizationAsync(Guid organizationId);

    /// <summary>
    /// Finds a user by their unique identifier.
    /// </summary>
    Task<UserDto?> GetUserByIdAsync(string id);

    /// <summary>
    /// Updates the specified user's profile and roles.
    /// </summary>
    Task<bool> UpdateUserAsync(UpdateUserDto request);

    /// <summary>
    /// Deletes a user from the system permanently.
    /// </summary>
    Task<bool> DeleteUserAsync(string id);

    /// <summary>
    /// Creates a new user with the specified credentials and roles.
    /// </summary>
    Task<bool> CreateUserAsync(CreateUserDto request);
    
    /// <summary>
    /// Validates current password and sends an OTP to initiate a change.
    /// </summary>
    Task<bool> InitiatePasswordChangeAsync(string userId, ChangePasswordRequestDto request);

    /// <summary>
    /// Compares the provided OTP and updates the password if valid.
    /// </summary>
    Task<bool> CompletePasswordChangeAsync(string userId, VerifyPasswordChangeDto request);

    /// <summary>
    /// Ensures a user has a specific leadership role while removing conflicting roles (ex: CEO, Maker, Approver).
    /// </summary>
    Task<bool> SyncLeadingRoleAsync(string userId, string roleName, CancellationToken ct = default);
}
