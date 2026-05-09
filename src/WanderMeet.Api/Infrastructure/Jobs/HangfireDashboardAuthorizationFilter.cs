using Hangfire.Dashboard;

namespace WanderMeet.Api.Infrastructure.Jobs;

/// <summary>
/// Dashboard authorization filter that grants access only to authenticated users.
/// Read-only access by default; admin-grade trigger/retry actions are out of MVP scope.
/// </summary>
internal sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    /// <summary>
    /// Returns <see langword="true"/> when the current HTTP request has an authenticated user identity;
    /// <see langword="false"/> for anonymous or unauthenticated requests.
    /// </summary>
    public bool Authorize(DashboardContext context)
        => context.GetHttpContext().User.Identity?.IsAuthenticated == true;
}
