using FMC.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace FMC.Infrastructure.Services;

public class AuthRateLimitService : IAuthRateLimitService
{
    private readonly ICacheService _cache;
    private readonly ILogger<AuthRateLimitService> _logger;

    private const int LockoutThreshold = 4;
    private static readonly int[] BackoffLevels = { 60, 300, 900, 1800 };

    private static readonly Dictionary<string, (int MaxAttempts, int WindowSeconds)> Policies = new()
    {
        [RateLimitPolicies.Login] = (5, 60),
        [RateLimitPolicies.ForgotPassword] = (3, 300),
        [RateLimitPolicies.ResetPassword] = (3, 300),
        [RateLimitPolicies.ChangePassword] = (3, 300),
    };

    private static string AttemptsKey(string id, string policy) => $"rate_limit:{id}:{policy}:attempts";
    private static string StrikesKey(string id) => $"rate_limit:{id}:strikes";
    private static string LockoutKey(string id) => $"rate_limit:{id}:lockout";

    public AuthRateLimitService(ICacheService cache, ILogger<AuthRateLimitService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<AuthRateLimitResult> CheckAsync(string clientId, string policy = "login")
    {
        var lockoutExpiry = await _cache.GetAsync<string>(LockoutKey(clientId));
        if (lockoutExpiry != null && long.TryParse(lockoutExpiry, out var lockoutTs))
        {
            var lockoutTime = DateTimeOffset.FromUnixTimeSeconds(lockoutTs);
            var remaining = (int)(lockoutTime.UtcDateTime - DateTime.UtcNow).TotalSeconds;
            if (remaining > 0)
                return new AuthRateLimitResult(false, remaining, LockoutReason: "Account temporarily locked. Too many repeated violations.");
        }

        if (!Policies.TryGetValue(policy, out var config))
            return new AuthRateLimitResult(true, 0);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - config.WindowSeconds;

        var attempts = await _cache.GetAsync<List<long>>(AttemptsKey(clientId, policy));
        var filteredAttempts = attempts?
            .Where(t => t >= windowStart)
            .ToList() ?? new List<long>();

        var attemptsRemaining = config.MaxAttempts - filteredAttempts.Count;
        if (attemptsRemaining > 0)
            return new AuthRateLimitResult(true, 0, attemptsRemaining);

        var strikes = await _cache.GetAsync<int>(StrikesKey(clientId));
        var level = Math.Min(strikes, BackoffLevels.Length - 1);
        var cooldown = BackoffLevels[level];
        var totalStrikes = strikes + 1;

        _logger.LogWarning("Rate limit exceeded for {ClientId} on policy {Policy}. Strike {Strikes}, cooldown {Cooldown}s",
            clientId, policy, totalStrikes, cooldown);

        return new AuthRateLimitResult(false, cooldown, 0,
            totalStrikes >= LockoutThreshold ? "Account locked due to repeated violations." : null);
    }

    public async Task RecordAttemptAsync(string clientId, bool success, string policy = "login")
    {
        if (success)
        {
            await ClearAsync(clientId);
            return;
        }

        if (!Policies.TryGetValue(policy, out var config))
            return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - config.WindowSeconds;

        var attempts = await _cache.GetAsync<List<long>>(AttemptsKey(clientId, policy));
        attempts = attempts?
            .Where(t => t >= windowStart)
            .ToList() ?? new List<long>();

        attempts.Add(now);
        await _cache.SetAsync(AttemptsKey(clientId, policy), attempts, TimeSpan.FromSeconds(config.WindowSeconds * 2));

        if (attempts.Count >= config.MaxAttempts)
        {
            var strikes = await _cache.GetAsync<int>(StrikesKey(clientId)) + 1;
            var level = Math.Min(strikes - 1, BackoffLevels.Length - 1);
            var cooldown = BackoffLevels[level];
            var lockoutExpiry = DateTimeOffset.UtcNow.AddSeconds(cooldown).ToUnixTimeSeconds();

            await _cache.SetAsync(StrikesKey(clientId), strikes, TimeSpan.FromHours(1));
            await _cache.SetAsync(LockoutKey(clientId), lockoutExpiry.ToString(), TimeSpan.FromSeconds(cooldown + 10));

            if (strikes >= LockoutThreshold)
                _logger.LogWarning("Account {ClientId} LOCKED after {Strikes} strikes", clientId, strikes);
        }
    }

    public async Task ClearAsync(string clientId)
    {
        foreach (var policy in Policies.Keys)
            await _cache.RemoveAsync(AttemptsKey(clientId, policy));
        await _cache.RemoveAsync(StrikesKey(clientId));
        await _cache.RemoveAsync(LockoutKey(clientId));
    }
}