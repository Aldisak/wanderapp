using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Users.UpdateFcmToken;

/// <summary>Integration tests for PATCH /api/v1/users/me/fcm-token.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class UpdateFcmTokenEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private const string ValidToken = "fcm-token-abc123xyz";

    private async Task<(Guid UserId, HttpClient Client)> SeedUserAndCreateClientAsync(
        string sub,
        string forwardedFor)
    {
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid userId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            userId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = userId,
                AzureAdB2CId = sub,
                FirstName = "Alice",
                LastActiveAt = now,
                TrustScore = 0,
                MeetupCount = 0,
                CitiesCount = 0,
                CreatedAt = now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(sub);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);

        return (userId, client);
    }

    /// <summary>Happy path: valid token → 204, FcmToken and LastActiveAt are persisted.</summary>
    [Fact]
    public async Task HandleAsync_ValidRequest_Returns204AndSetsFcmToken()
    {
        const string SUB = "oid|fcm-happy";
        var (userId, client) = await SeedUserAndCreateClientAsync(SUB, "10.80.0.1");

        var response = await client.PatchAsJsonAsync(
            "api/v1/users/me/fcm-token",
            new { Token = ValidToken },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, TestContext.Current.CancellationToken);

        user.Should().NotBeNull();
        user!.FcmToken.Should().Be(ValidToken);
        user.LastActiveAt.Should().Be(App.FakeTimeProvider.GetUtcNow());
    }

    /// <summary>Posting the same token twice succeeds; LastActiveAt advances on second call.</summary>
    [Fact]
    public async Task HandleAsync_ResendSameToken_Returns204AndAdvancesLastActiveAt()
    {
        const string SUB = "oid|fcm-resend";
        var (userId, client) = await SeedUserAndCreateClientAsync(SUB, "10.80.0.2");

        // First PATCH
        var first = await client.PatchAsJsonAsync(
            "api/v1/users/me/fcm-token",
            new { Token = ValidToken },
            TestContext.Current.CancellationToken);
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var firstTime = App.FakeTimeProvider.GetUtcNow();

        // Advance the clock so LastActiveAt changes
        App.FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));

        // Second PATCH (same token)
        var second = await client.PatchAsJsonAsync(
            "api/v1/users/me/fcm-token",
            new { Token = ValidToken },
            TestContext.Current.CancellationToken);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, TestContext.Current.CancellationToken);

        user!.FcmToken.Should().Be(ValidToken);
        user.LastActiveAt.Should().BeAfter(firstTime);
        user.LastActiveAt.Should().Be(App.FakeTimeProvider.GetUtcNow());
    }

    /// <summary>Posting a different token replaces the previous value.</summary>
    [Fact]
    public async Task HandleAsync_DifferentToken_OverwritesPreviousToken()
    {
        const string SUB = "oid|fcm-overwrite";
        var (userId, client) = await SeedUserAndCreateClientAsync(SUB, "10.80.0.3");

        const string firstToken = "first-fcm-token";
        const string secondToken = "second-fcm-token";

        await client.PatchAsJsonAsync(
            "api/v1/users/me/fcm-token",
            new { Token = firstToken },
            TestContext.Current.CancellationToken);

        var response = await client.PatchAsJsonAsync(
            "api/v1/users/me/fcm-token",
            new { Token = secondToken },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, TestContext.Current.CancellationToken);

        user!.FcmToken.Should().Be(secondToken);
    }

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.80.0.4");

        var response = await client.PatchAsJsonAsync(
            "api/v1/users/me/fcm-token",
            new { Token = ValidToken },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Valid token but no user row → 404 with User.NotRegistered.</summary>
    [Fact]
    public async Task HandleAsync_CallerNotRegistered_Returns404WithUserNotRegistered()
    {
        const string SUB = "oid|fcm-no-user";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.80.0.5");

        var response = await client.PatchAsJsonAsync(
            "api/v1/users/me/fcm-token",
            new { Token = ValidToken },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("User.NotRegistered");
    }

    /// <summary>Empty token → 400 with Validation.FcmTokenRequired.</summary>
    [Fact]
    public async Task HandleAsync_EmptyToken_Returns400WithFcmTokenRequired()
    {
        const string SUB = "oid|fcm-empty-token";
        var (_, client) = await SeedUserAndCreateClientAsync(SUB, "10.80.0.6");

        var response = await client.PatchAsJsonAsync(
            "api/v1/users/me/fcm-token",
            new { Token = "" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Validation.FcmTokenRequired");
    }

    /// <summary>Whitespace-only token → 400 with Validation.FcmTokenRequired.</summary>
    [Fact]
    public async Task HandleAsync_WhitespaceOnlyToken_Returns400WithFcmTokenRequired()
    {
        const string SUB = "oid|fcm-whitespace-token";
        var (_, client) = await SeedUserAndCreateClientAsync(SUB, "10.80.0.7");

        var response = await client.PatchAsJsonAsync(
            "api/v1/users/me/fcm-token",
            new { Token = "   " },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Validation.FcmTokenRequired");
    }

    /// <summary>Token longer than 512 chars → 400 with Validation.FcmTokenTooLong.</summary>
    [Fact]
    public async Task HandleAsync_TokenLongerThanMax_Returns400WithFcmTokenTooLong()
    {
        const string SUB = "oid|fcm-toolong-token";
        var (_, client) = await SeedUserAndCreateClientAsync(SUB, "10.80.0.8");

        var tooLong = new string('x', 513);

        var response = await client.PatchAsJsonAsync(
            "api/v1/users/me/fcm-token",
            new { Token = tooLong },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Validation.FcmTokenTooLong");
    }
}
