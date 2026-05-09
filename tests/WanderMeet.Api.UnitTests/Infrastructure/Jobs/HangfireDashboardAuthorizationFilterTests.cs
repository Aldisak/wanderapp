using System.Security.Claims;
using FakeItEasy;
using FluentAssertions;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WanderMeet.Api.Infrastructure.Jobs;
using Xunit;

namespace WanderMeet.Api.UnitTests.Infrastructure.Jobs;

/// <summary>
/// Unit tests for <see cref="HangfireDashboardAuthorizationFilter"/>. Verifies the dashboard
/// is admin-only — both anonymous and authenticated-non-admin requests are rejected.
/// </summary>
public class HangfireDashboardAuthorizationFilterTests
{
    private static HangfireDashboardAuthorizationFilter CreateSut() => new();

    private static DashboardContext BuildContext(HttpContext httpContext)
    {
        // AspNetCoreDashboardContext ctor calls httpContext.RequestServices.GetService<IHangfireContextAccessor>()
        httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();

        var fakeStorage = A.Fake<JobStorage>();
        return new AspNetCoreDashboardContext(fakeStorage, new DashboardOptions(), httpContext);
    }

    private static HttpContext WithUser(ClaimsPrincipal principal)
    {
        var ctx = new DefaultHttpContext { User = principal };
        return ctx;
    }

    /// <summary>Anonymous / unauthenticated requests must be blocked.</summary>
    [Fact]
    public void Authorize_AnonymousUser_ReturnsFalse()
    {
        var httpContext = WithUser(new ClaimsPrincipal());
        var dashboardContext = BuildContext(httpContext);

        var result = CreateSut().Authorize(dashboardContext);

        result.Should().BeFalse(because: "anonymous requests must be rejected");
    }

    /// <summary>Authenticated users WITHOUT the Admin role must be blocked.</summary>
    [Fact]
    public void Authorize_AuthenticatedUserWithoutAdminRole_ReturnsFalse()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "alice")],
            authenticationType: "TestAuth"));
        var dashboardContext = BuildContext(WithUser(principal));

        var result = CreateSut().Authorize(dashboardContext);

        result.Should().BeFalse(because: "regular authenticated users must NOT see Hangfire telemetry");
    }

    /// <summary>Authenticated users WITH the Admin role must be granted access.</summary>
    [Fact]
    public void Authorize_AuthenticatedUserWithAdminRole_ReturnsTrue()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Role, HangfireDashboardAuthorizationFilter.AdminRole),
            ],
            authenticationType: "TestAuth"));
        var dashboardContext = BuildContext(WithUser(principal));

        var result = CreateSut().Authorize(dashboardContext);

        result.Should().BeTrue(because: "users with the Admin role must be allowed in");
    }
}
