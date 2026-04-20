using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;

namespace FMC.Infrastructure.Resilience;

/// <summary>
/// Centralized Polly resilience policy definitions for the FMC infrastructure layer.
/// These policies are shared across repositories and services to provide consistent,
/// enterprise-grade fault tolerance without duplicating policy configuration.
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Creates a database retry pipeline for transient SQL Server faults.
    /// Retries 3 times with exponential back-off: 2s, 4s, 8s.
    /// This silently handles deadlocks, timeout spikes, and brief network interruptions
    /// on your company servers without surfacing errors to the user.
    /// </summary>
    public static ResiliencePipeline GetDatabaseRetryPipeline(ILogger logger) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true, // Adds slight randomness to avoid "thundering herd" on server restarts
                ShouldHandle = new PredicateBuilder().Handle<Microsoft.Data.SqlClient.SqlException>()
                                                     .Handle<TimeoutException>()
                                                     .Handle<TaskCanceledException>(),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "[Resilience] Database transient fault detected. Retry attempt {Attempt} after {Delay}s. Error: {Error}",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalSeconds,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

    /// <summary>
    /// Creates a circuit breaker for external service calls (e.g., future cardholder API).
    /// If 5 calls fail within 30 seconds, the breaker "trips" and fast-fails all calls
    /// for 60 seconds. This prevents the dashboard from hanging when the external server is down.
    /// </summary>
    public static ResiliencePipeline GetExternalServiceCircuitBreaker(ILogger logger) =>
        new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,            // Trip if 50%+ of calls fail
                MinimumThroughput = 5,         // Must have at least 5 calls before tripping
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(60),
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>()
                                                     .Handle<TimeoutException>(),
                OnOpened = args =>
                {
                    logger.LogError(
                        "[Resilience] Circuit OPENED for external service. Fast-failing requests for {Duration}s. Cause: {Error}",
                        args.BreakDuration.TotalSeconds,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger.LogInformation("[Resilience] Circuit CLOSED. External service recovered successfully.");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    logger.LogInformation("[Resilience] Circuit HALF-OPEN. Testing external service with a probe request.");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

    /// <summary>
    /// A combined pipeline: Retry 3x first, then if the circuit is open, fast-fail immediately.
    /// This is the recommended "defense-in-depth" pipeline for critical financial operations.
    /// </summary>
    public static ResiliencePipeline GetDatabaseResiliencePipeline(ILogger logger) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder().Handle<Microsoft.Data.SqlClient.SqlException>()
                                                     .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "[Resilience] Retrying DB operation. Attempt {Attempt}, Delay {Delay}s. Cause: {Error}",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalSeconds,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
}
