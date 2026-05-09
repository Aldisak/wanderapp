using Hangfire.Dashboard;

namespace WanderMeet.Api.Infrastructure.Jobs;

/// <summary>
/// Dashboard authorization filter that grants access only to users in the Admin role.
/// Authenticated end-users are NOT permitted; the dashboard exposes operational data
/// (job arguments, queue state) that must remain admin-only.
/// </summary>
/// <remarks>
/// No users currently hold the Admin role — the role-assignment flow is a future UC.
/// Until that lands the dashboard is effectively closed in production, which is the
/// safe default per the security audit (finding F1).
/// </remarks>
internal sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    /// <summary>Admin role-claim value the JWT must carry to access the dashboard.</summary>
    public const string AdminRole = "Admin";

    /// <summary>
    /// Returns <see langword="true"/> when the current request carries an authenticated
    /// principal with the <see cref="AdminRole"/> role; <see langword="false"/> otherwise.
    /// </summary>
    public bool Authorize(DashboardContext context)
    {
        var user = context.GetHttpContext().User;
        return user.Identity?.IsAuthenticated == true && user.IsInRole(AdminRole);
    }
}
