using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Reports.SubmitReport;

/// <summary>Integration tests for POST /api/v1/reports.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class SubmitReportEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static User MakeUser(string sub, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        AzureAdB2CId = sub,
        FirstName = "User " + sub,
        CityId = null,
        CreatedAt = now,
        LastActiveAt = now,
    };

    private async Task<Guid> SeedUserAsync(string sub, DateTimeOffset now, bool softDeleted = false)
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var user = MakeUser(sub, now);
        if (softDeleted) user.DeletedAt = now;
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user.Id;
    }

    // -----------------------------------------------------------------------
    // Happy path
    // -----------------------------------------------------------------------

    /// <summary>Valid request → 204, Report row persisted, caller.LastActiveAt updated.</summary>
    [Fact]
    public async Task HandleAsync_ValidRequest_Returns204AndPersistsReport()
    {
        const string CALLER_SUB = "oid|report-happy-caller";
        const string TARGET_SUB = "oid|report-happy-target";
        var now = App.FakeTimeProvider.GetUtcNow();

        var callerId = await SeedUserAsync(CALLER_SUB, now);
        var targetId = await SeedUserAsync(TARGET_SUB, now);

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.60.1.1");

        var reason = new string('a', 100);
        var response = await client.PostAsJsonAsync(
            "api/v1/reports",
            new { ReportedUserId = targetId, Reason = reason },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().BeEmpty();

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();

        var report = await db.Reports.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReporterId == callerId && r.ReportedId == targetId,
                TestContext.Current.CancellationToken);

        report.Should().NotBeNull();
        report!.ReporterId.Should().Be(callerId);
        report.ReportedId.Should().Be(targetId);
        report.Reason.Should().Be(reason.Trim());
        report.CreatedAt.Should().Be(now);
        report.ReviewedAt.Should().BeNull();

        var caller = await db.Users.AsNoTracking()
            .FirstAsync(u => u.Id == callerId, TestContext.Current.CancellationToken);
        caller.LastActiveAt.Should().Be(now);
    }

    /// <summary>Two back-to-back reports against the same target both persist (no unique constraint on duplicate reports).</summary>
    [Fact]
    public async Task HandleAsync_DuplicateReportSameTarget_BothPersist()
    {
        const string CALLER_SUB = "oid|report-dup-caller";
        const string TARGET_SUB = "oid|report-dup-target";
        var now = App.FakeTimeProvider.GetUtcNow();

        var callerId = await SeedUserAsync(CALLER_SUB, now);
        var targetId = await SeedUserAsync(TARGET_SUB, now);

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.60.1.2");

        var body1 = await client.PostAsJsonAsync(
            "api/v1/reports",
            new { ReportedUserId = targetId, Reason = "First report" },
            TestContext.Current.CancellationToken);
        var body2 = await client.PostAsJsonAsync(
            "api/v1/reports",
            new { ReportedUserId = targetId, Reason = "Second report" },
            TestContext.Current.CancellationToken);

        body1.StatusCode.Should().Be(HttpStatusCode.NoContent);
        body2.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var count = await db.Reports
            .Where(r => r.ReporterId == callerId && r.ReportedId == targetId)
            .CountAsync(TestContext.Current.CancellationToken);
        count.Should().Be(2);
    }

    // -----------------------------------------------------------------------
    // Auth (401)
    // -----------------------------------------------------------------------

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.60.1.3");

        var response = await client.PostAsJsonAsync(
            "api/v1/reports",
            new { ReportedUserId = Guid.NewGuid(), Reason = "Some reason" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -----------------------------------------------------------------------
    // Not-found (404) — caller not registered
    // -----------------------------------------------------------------------

    /// <summary>JWT sub has no User row → 404 + User.NotRegistered.</summary>
    [Fact]
    public async Task HandleAsync_JwtSubHasNoUserRow_Returns404WithUserNotRegistered()
    {
        const string SUB = "oid|report-no-user-row";

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.60.1.4");

        var response = await client.PostAsJsonAsync(
            "api/v1/reports",
            new { ReportedUserId = Guid.NewGuid(), Reason = "Some reason" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.User.NotRegistered);
    }

    // -----------------------------------------------------------------------
    // Not-found (404) — target user
    // -----------------------------------------------------------------------

    /// <summary>Reported user id does not exist → 404 + Report.UserNotFound.</summary>
    [Fact]
    public async Task HandleAsync_ReportedUserNotFound_Returns404WithReportUserNotFound()
    {
        const string CALLER_SUB = "oid|report-target-missing-caller";
        var now = App.FakeTimeProvider.GetUtcNow();

        await SeedUserAsync(CALLER_SUB, now);

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.60.1.5");

        var response = await client.PostAsJsonAsync(
            "api/v1/reports",
            new { ReportedUserId = Guid.NewGuid(), Reason = "Some reason" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Report.UserNotFound");
    }

    /// <summary>Reported user is soft-deleted → 404 + Report.UserNotFound.</summary>
    [Fact]
    public async Task HandleAsync_ReportedUserSoftDeleted_Returns404WithReportUserNotFound()
    {
        const string CALLER_SUB = "oid|report-target-deleted-caller";
        const string TARGET_SUB = "oid|report-target-deleted-target";
        var now = App.FakeTimeProvider.GetUtcNow();

        await SeedUserAsync(CALLER_SUB, now);
        var targetId = await SeedUserAsync(TARGET_SUB, now, softDeleted: true);

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.60.1.6");

        var response = await client.PostAsJsonAsync(
            "api/v1/reports",
            new { ReportedUserId = targetId, Reason = "Some reason" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Report.UserNotFound");
    }

    // -----------------------------------------------------------------------
    // Bad request (400) — business guards
    // -----------------------------------------------------------------------

    /// <summary>Caller reports themselves → 400 + Report.SelfReportForbidden.</summary>
    [Fact]
    public async Task HandleAsync_SelfReport_Returns400WithSelfReportForbidden()
    {
        const string CALLER_SUB = "oid|report-self-caller";
        var now = App.FakeTimeProvider.GetUtcNow();

        var callerId = await SeedUserAsync(CALLER_SUB, now);

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.60.1.7");

        var response = await client.PostAsJsonAsync(
            "api/v1/reports",
            new { ReportedUserId = callerId, Reason = "Some reason" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Report.SelfReportForbidden");
    }

    // -----------------------------------------------------------------------
    // Bad request (400) — validator paths via HTTP
    // -----------------------------------------------------------------------

    /// <summary>Reason is 301 chars → 400 + Validation.ReportReasonTooLong.</summary>
    [Fact]
    public async Task HandleAsync_ReasonTooLong_Returns400WithReportReasonTooLong()
    {
        const string CALLER_SUB = "oid|report-reason-long-caller";
        var now = App.FakeTimeProvider.GetUtcNow();

        await SeedUserAsync(CALLER_SUB, now);

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.60.1.8");

        var response = await client.PostAsJsonAsync(
            "api/v1/reports",
            new { ReportedUserId = Guid.NewGuid(), Reason = new string('x', ValidationConstants.ReportReasonMaxLength + 1) },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Validation.ReportReasonTooLong);
    }

    /// <summary>Reason is whitespace-only → 400 + Validation.ReportReasonRequired.</summary>
    [Fact]
    public async Task HandleAsync_ReasonEmpty_Returns400WithReportReasonRequired()
    {
        const string CALLER_SUB = "oid|report-reason-empty-caller";
        var now = App.FakeTimeProvider.GetUtcNow();

        await SeedUserAsync(CALLER_SUB, now);

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.60.1.9");

        var response = await client.PostAsJsonAsync(
            "api/v1/reports",
            new { ReportedUserId = Guid.NewGuid(), Reason = "   " },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Validation.ReportReasonRequired);
    }

    // -----------------------------------------------------------------------
    // Rate limit (429)
    // -----------------------------------------------------------------------

    /// <summary>6th report exceeds the 5/day quota → 429 + Retry-After header.</summary>
    [Fact]
    public async Task HandleAsync_ExceedsDailyQuota_Returns429WithRetryAfter()
    {
        const string RATE_LIMIT_IP = "10.60.99.1";
        const string CALLER_SUB = "oid|report-ratelimit-caller-unique";
        var now = App.FakeTimeProvider.GetUtcNow();

        var callerId = await SeedUserAsync(CALLER_SUB, now);

        // Seed 6 distinct targets
        var targetIds = new List<Guid>();
        for (var i = 0; i < 6; i++)
        {
            var targetId = await SeedUserAsync($"oid|report-ratelimit-target-{i}", now);
            targetIds.Add(targetId);
        }

        // IMPORTANT: one client instance for all 6 calls (per CLAUDE.md rate-limit isolation rule)
        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", RATE_LIMIT_IP);

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 6; i++)
        {
            lastResponse = await client.PostAsJsonAsync(
                "api/v1/reports",
                new { ReportedUserId = targetIds[i], Reason = "Reason " + i },
                TestContext.Current.CancellationToken);

            if (i < 5)
                lastResponse.StatusCode.Should().Be(HttpStatusCode.NoContent, $"call {i + 1} of 5 should succeed");
        }

        lastResponse.Should().NotBeNull();
        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        lastResponse.Headers.Contains("Retry-After").Should().BeTrue();
    }
}
