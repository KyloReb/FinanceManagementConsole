using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FMC.Api.Middleware;

/// <summary>
/// Global exception handler that catches all unhandled exceptions from controllers
/// and returns a standardized JSON error envelope. Replaces the default ASP.NET Core
/// HTML error page so that API consumers always receive a parseable response.
/// 
/// Maps common exception types to appropriate HTTP status codes:
///   • UnauthorizedAccessException    → 401
///   • KeyNotFoundException           → 404
///   • ArgumentException / FormatException → 400
///   • All others                     → 500 (with generic message in production)
/// 
/// The error envelope includes a <c>correlationId</c> (TraceIdentifier) to make
/// debugging across logs feasible without exposing internal stack traces.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IHostEnvironment _env;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IHostEnvironment env, ILogger<GlobalExceptionHandler> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = context.TraceIdentifier;

        // ── Map exception type → HTTP status code and client-safe title ──
        var (statusCode, title) = exception switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            KeyNotFoundException        => (StatusCodes.Status404NotFound, "Resource Not Found"),
            ArgumentException           => (StatusCodes.Status400BadRequest, "Bad Request"),
            FormatException             => (StatusCodes.Status400BadRequest, "Bad Request"),
            InvalidOperationException   => (StatusCodes.Status409Conflict, "Conflict"),
            _                           => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        // ── Log every unhandled exception with correlation ID for traceability ──
        _logger.LogError(exception, "[{CorrelationId}] Unhandled exception — {Title} ({StatusCode})", correlationId, title, statusCode);

        // ── Build standardized error envelope ──
        // In development we include the exception message for easier debugging.
        // In production we return a generic message for 500-level errors.
        var detail = _env.IsDevelopment() || statusCode < 500
            ? exception.Message
            : "An unexpected error occurred. Please try again later.";

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var envelope = new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{statusCode}",
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = context.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
                ["timestamp"] = DateTime.UtcNow.ToString("O")
            }
        };

        await context.Response.WriteAsJsonAsync(envelope, cancellationToken);
        return true; // Signal that the exception was handled
    }
}
