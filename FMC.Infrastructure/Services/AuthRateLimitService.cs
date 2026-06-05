using FMC.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace FMC.Infrastructure.Services;

public class AuthRateLimitService : IAuthRateLimitService
{
    private readonly ICacheService _cache;
    private readonly ILogger<AuthRateLimitService> _logger;

    private const int MaxAttempts = 5;
    private static readonly TimeSpan WindowSize = TimeSpan.FromMinutes(1);
    private static readonly int[] BackoffLevels = { 60, 300, 900, 1800 };
    private const int LockoutThreshold = 4;

    private static string AttemptsKey(string id) => $"rate_limit:{id}:attempts";
    private static string StrikesKey(string id) => $"rate_limit:{id}:strikes";
    private static string LockoutKey(string id) => $"rate_limit:{id}:lockout";

    public AuthRateLimitService(ICacheService cache, ILogger<AuthRateLimitService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<AuthRateLimitResult> CheckAsync(string clientId)
    {
        var lockoutExpiry = await _cache.GetAsync<string>(LockoutKey(clientId));
        if (lockoutExpiry != null && long.TryParse(lockoutExpiry, out var lockoutTs))
        {
            var lockoutTime = DateTimeOffset.FromUnixTimeSeconds(lockoutTs);
            var remaining = (int)(lockoutTime.UtcDateTime - DateTime.UtcNow).TotalSeconds;
            if (remaining > 0)
                return new AuthRateLimitResult(false, remaining, "Account temporarily locked. Too many repeated violations.");
        }

        var attemptsRaw = await _cache.GetAsync<string>(AttemptsKey(clientId));
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - (long)WindowSize.TotalSeconds;

        var attempts = string.IsNullOrEmpty(attemptsRaw)
            ? new List<long>()
            : System.Text.Json.JsonSerializer.Deserialize<List<long>>(attemptsRaw)?
                .Where(t => t >= windowStart).ToList() ?? new List<long>();

        if (attempts.Count < MaxAttempts)
            return new AuthRateLimitResult(true, 0);

        var strikes = await _cache.GetAsync<int>(StrikesKey(clientId));
        var level = Math.Min(strikes, BackoffLevels.Length - 1);
        var cooldown = BackoffLevels[level];
        var totalStrikes = strikes + 1;

        _logger.LogWarning("Rate limit exceeded for {ClientId}. Strike {Strikes}, cooldown {Cooldown}s", clientId, totalStrikes, cooldown);

        return new AuthRateLimitResult(false, cooldown, totalStrikes >= LockoutThreshold ? "Account locked due to repeated violations." : null);
    }

    public async Task RecordAttemptAsync(string clientId, bool success)
    {
        if (success)
        {
            await ClearAsync(clientId);
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - (long)WindowSize.TotalSeconds;

        var attemptsRaw = await _cache.GetAsync<string>(AttemptsKey(clientId));
        var attempts = string.IsNullOrEmpty(attemptsRaw)
            ? new List<long>()
            : System.Text.Json.JsonSerializer.Deserialize<List<long>>(attemptsRaw)?
                .Where(t => t >= windowStart).ToList() ?? new List<long>();

        attempts.Add(now);
        await _cache.SetAsync(AttemptsKey(clientId), attempts, WindowSize * 2);

        if (attempts.Count >= MaxAttempts)
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
        await _cache.RemoveAsync(AttemptsKey(clientId));
        await _cache.RemoveAsync(StrikesKey(clientId));
        await _cache.RemoveAsync(LockoutKey(clientId));
    }
}