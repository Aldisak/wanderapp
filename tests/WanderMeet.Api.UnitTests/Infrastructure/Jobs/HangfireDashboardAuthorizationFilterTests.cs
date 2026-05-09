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
/// Unit tests for <see cref="HangfireDashboardAuthorizationFilter"/>.
/// <para>
/// <see cref="DashboardContext"/> is abstract with a protected ctor.
/// <see cref="AspNetCoreDashboardContext"/> (from Hangfire.AspNetCore, in the <c>Hangfire.Dashboard</c> namespace)
/// has a public ctor <c>(JobStorage, DashboardOptions, HttpContext)</c>. The ctor internally calls
/// <c>httpContext.RequestServices.GetService&lt;IHangfireContextAccessor&gt;()</c>, so we must set
/// <c>httpContext.RequestServices</c> to a minimal <see cref="ServiceProvider"/>.
/// <see cref="JobStorage"/> is abstract — we use <c>A.Fake&lt;JobStorage&gt;()</c> (FakeItEasy).
/// </para>
/// </summary>
public class HangfireDashboardAuthorizationFilterTests
{
    private static HangfireDashboardAuthorizationFilter CreateSut() => new();

    private static DashboardContext BuildContext(HttpContext httpContext)
    {
        // AspNetCoreDashboardContext ctor requires httpContext.RequestServices to be non-null
        // because it calls GetService<IHangfireContextAccessor>(). We provide an empty container.
        httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();

        var fakeStorage = A.Fake<JobStorage>();
        return new AspNetCoreDashboardContext(fakeStorage, new DashboardOptions(), httpContext);
    }

    /// <summary>Authenticated users must be granted access to the dashboard.</summary>
    [Fact]
    public void Authorize_AuthenticatedUser_ReturnsTrue()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "alice")],
                authenticationType: "TestAuth"));

        var dashboardContext = BuildContext(httpContext);
        var sut = CreateSut();

        // Act
        var result = sut.Authorize(dashboardContext);

        // Assert
        result.Should().BeTrue(because: "an authenticated user must be allowed through");
    }

    /// <summary>Anonymous / unauthenticated requests must be blocked from the dashboard.</summary>
    [Fact]
    public void Authorize_AnonymousUser_ReturnsFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        // Default ClaimsPrincipal has an empty/unauthenticated identity

        var dashboardContext = BuildContext(httpContext);
        var sut = CreateSut();

        // Act
        var result = sut.Authorize(dashboardContext);

        // Assert
        result.Should().BeFalse(because: "an unauthenticated request must be blocked from the dashboard");
    }
}
