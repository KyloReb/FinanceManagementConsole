namespace FMC.Application.Interfaces;

public interface IAuthRateLimitService
{
    Task<AuthRateLimitResult> CheckAsync(string clientId, string policy = "login");
    Task RecordAttemptAsync(string clientId, bool success, string policy = "login");
    Task ClearAsync(string clientId);
}

public record AuthRateLimitResult(bool IsAllowed, int RetryAfterSeconds, string? LockoutReason = null);

public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string ForgotPassword = "forgot-password";
    public const string ResetPassword = "reset-password";
    public const string ChangePassword = "change-password";
}