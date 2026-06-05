namespace FMC.Application.Interfaces;

public interface IAuthRateLimitService
{
    Task<AuthRateLimitResult> CheckAsync(string clientId);
    Task RecordAttemptAsync(string clientId, bool success);
    Task ClearAsync(string clientId);
}

public record AuthRateLimitResult(bool IsAllowed, int RetryAfterSeconds, string? LockoutReason = null);