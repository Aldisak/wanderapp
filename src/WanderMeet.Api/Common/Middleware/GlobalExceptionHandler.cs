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
        logger.LogError(
            exception,
            "Unhandled exception while processing {Method} {Path} (TraceId={TraceId})",
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.TraceIdentifier);

        return ValueTask.FromResult(false);
    }
}
