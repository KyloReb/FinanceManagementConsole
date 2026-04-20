using FMC.Application.Interfaces;
using Hangfire;
using System.Linq.Expressions;

namespace FMC.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire-backed implementation of <see cref="IBackgroundJobService"/>.
/// Provides enterprise-grade job persistence using your company's SQL Server database.
/// 
/// All jobs are automatically retried on failure (default: 10 attempts with exponential back-off).
/// Jobs persist across application restarts — if the server goes down mid-send, Hangfire will
/// resume the job when the server comes back online. No emails or reports will be "lost".
/// </summary>
public sealed class HangfireBackgroundJobService : IBackgroundJobService
{
    private readonly IBackgroundJobClient _client;
    private readonly IRecurringJobManager _recurringJobManager;

    /// <summary>
    /// Initializes a new instance of <see cref="HangfireBackgroundJobService"/>.
    /// </summary>
    public HangfireBackgroundJobService(
        IBackgroundJobClient client,
        IRecurringJobManager recurringJobManager)
    {
        _client = client;
        _recurringJobManager = recurringJobManager;
    }

    /// <inheritdoc />
    public string Enqueue<T>(Expression<Action<T>> methodCall) =>
        _client.Enqueue(methodCall);

    /// <inheritdoc />
    public string Enqueue<T>(Expression<Func<T, Task>> methodCall) =>
        _client.Enqueue(methodCall);

    /// <inheritdoc />
    public string Schedule<T>(Expression<Action<T>> methodCall, TimeSpan delay) =>
        _client.Schedule(methodCall, delay);

    /// <inheritdoc />
    public string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay) =>
        _client.Schedule(methodCall, delay);

    /// <inheritdoc />
    public void AddOrUpdateRecurring<T>(string jobId, Expression<Action<T>> methodCall, string cronExpression) =>
        _recurringJobManager.AddOrUpdate(jobId, methodCall, cronExpression);
}
