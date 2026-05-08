using Serilog.Context;

namespace WanderMeet.Api.Common.Middleware;

/// <summary>
/// Reads (or generates) an <c>X-Correlation-ID</c>, mirrors it onto the response, sets
/// <see cref="HttpContext.TraceIdentifier"/> for downstream RFC 7807 ProblemDetails, and
/// pushes it into the Serilog log context so every log line in the request scope is tagged.
/// Must run before any middleware that emits logs or writes responses.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-ID";

    /// <summary>Apply the correlation id and forward.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId =
            context.Request.Headers.TryGetValue(HeaderName, out var inbound) &&
            !string.IsNullOrWhiteSpace(inbound.ToString())
                ? inbound.ToString()
                : Guid.NewGuid().ToString("N");

        context.Response.Headers[HeaderName] = correlationId;
        context.TraceIdentifier = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
