namespace WanderMeet.Api.Common.Middleware;

/// <summary>
/// Adds hardening response headers to every reply. Must run before any endpoint writes.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>Apply the headers and forward.</summary>
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
        return next(context);
    }
}
