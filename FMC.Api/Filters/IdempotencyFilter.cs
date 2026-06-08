using FMC.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FMC.Api.Filters;

/// <summary>
/// Global action filter that enforces idempotency for financial endpoints.
/// 
/// Reads the <c>Idempotency-Key</c> request header and checks if a transaction
/// with that key already exists in the database. If found, returns <c>409 Conflict</c>
/// with the existing transaction ID — preventing double-processing on network retry.
/// 
/// Apply via <c>[ServiceFilter(typeof(IdempotencyFilter))]</c> on individual endpoints
/// or register globally and skip with a custom marker attribute.
/// 
/// The filter uses <see cref="IOrganizationRepository.ExistsTransactionWithIdempotencyKeyAsync"/>
/// which is backed by a database unique index as the final safeguard.
/// </summary>
public class IdempotencyFilter : IAsyncActionFilter
{
    private readonly IOrganizationRepository _repository;
    private readonly ILogger<IdempotencyFilter> _logger;

    public IdempotencyFilter(IOrganizationRepository repository, ILogger<IdempotencyFilter> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Read the idempotency key from the request header.
        var key = context.HttpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();

        // If no key is present, skip the check — the endpoint either does not
        // require idempotency or the client chose not to send one (legacy callers).
        if (string.IsNullOrWhiteSpace(key))
        {
            await next();
            return;
        }

        // Check whether a transaction with this key has already been processed.
        var exists = await _repository.ExistsTransactionWithIdempotencyKeyAsync(key);

        if (exists)
        {
            _logger.LogWarning("Duplicate idempotency key rejected: {IdempotencyKey} from {RemoteIp}",
                key, context.HttpContext.Connection.RemoteIpAddress);

            context.Result = new ConflictObjectResult(new
            {
                success = false,
                error = new
                {
                    code = StatusCodes.Status409Conflict,
                    title = "Duplicate Request",
                    detail = $"A transaction with Idempotency-Key '{key}' has already been processed."
                }
            });
            return;
        }

        await next();
    }
}
