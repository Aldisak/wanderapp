using Microsoft.AspNetCore.Diagnostics;

namespace WanderMeet.Api.Common.Middleware;

/// <summary>
/// Logs unhandled exceptions and lets the framework's <c>ProblemDetails</c> writer emit the
/// RFC 7807 response body. Returning <c>false</c> keeps the customisation surface in
/// <c>AddProblemDetails(...)</c> and avoids duplicating the response shape here.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // CorrelationId is already on the LogContext via CorrelationIdMiddleware;
        // Path is the route path (no query string) so it doesn't leak token-style query params.
        logger.LogError(exception, "Request failed {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        return ValueTask.FromResult(false);
    }
}
