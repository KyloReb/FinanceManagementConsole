namespace FMC.Application.Interfaces;

/// <summary>
/// Abstraction over the background job scheduling infrastructure (e.g., Hangfire).
/// The Application layer depends on this interface — never directly on Hangfire.
/// This ensures that the background job engine can be swapped (e.g., from Hangfire
/// to Quartz.NET or a cloud queue) without modifying any business logic.
/// </summary>
public interface IBackgroundJobService
{
    /// <summary>
    /// Enqueues a fire-and-forget job to run as soon as a background worker is available.
    /// Use for non-critical tasks: email notifications, audit log enrichment, etc.
    /// </summary>
    /// <param name="methodCall">Expression pointing to the method to execute.</param>
    /// <returns>The unique job ID assigned by the job engine.</returns>
    string Enqueue<T>(System.Linq.Expressions.Expression<Action<T>> methodCall);

    /// <summary>
    /// Enqueues an asynchronous fire-and-forget job.
    /// </summary>
    string Enqueue<T>(System.Linq.Expressions.Expression<Func<T, Task>> methodCall);

    /// <summary>
    /// Schedules a delayed job to be executed after the specified time span.
    /// Use for deferred alerts, scheduled reminders, or SLA breach warnings.
    /// </summary>
    string Schedule<T>(System.Linq.Expressions.Expression<Action<T>> methodCall, TimeSpan delay);

    /// <summary>
    /// Schedules an asynchronous delayed job.
    /// </summary>
    string Schedule<T>(System.Linq.Expressions.Expression<Func<T, Task>> methodCall, TimeSpan delay);

    /// <summary>
    /// Registers or updates a recurring job that executes on a CRON schedule.
    /// Use for: nightly ledger summaries, weekly reporting, capacity threshold polling.
    /// </summary>
    /// <param name="jobId">Unique, descriptive job identifier (e.g., "fmc-nightly-ledger-summary").</param>
    /// <param name="methodCall">Expression pointing to the recurring method.</param>
    /// <param name="cronExpression">Standard CRON expression (e.g., "0 0 * * *" for midnight daily).</param>
    void AddOrUpdateRecurring<T>(string jobId, System.Linq.Expressions.Expression<Action<T>> methodCall, string cronExpression);
}
