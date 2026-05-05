using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Users.UpdateMe;

/// <summary>Integration tests for PATCH /api/v1/users/me.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class UpdateMeEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    /// <summary>Happy path: update bio + isOpenToRomance + hangoutTagIds, assert DB row matches.</summary>
    [Fact]
    public async Task HandleAsync_ValidRequest_Returns200AndUpdatesProfile()
    {
        const string SUB = "oid|updateme-happy";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid userId;
        Guid tag1Id;
        Guid tag2Id;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var tag1 = new HangoutTag { Id = Guid.NewGuid(), Slug = HangoutTagSlug.Coffee, Label = "Coffee", Emoji = "☕", CreatedAt = now };
            var tag2 = new HangoutTag { Id = Guid.NewGuid(), Slug = HangoutTagSlug.Walk, Label = "Walk", Emoji = "🚶", CreatedAt = now };
            db.HangoutTags.AddRange(tag1, tag2);
            tag1Id = tag1.Id;
            tag2Id = tag2.Id;

            userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                AzureAdB2CId = SUB,
                FirstName = "Bob",
                Bio = "Old bio",
                IsOpenToRomance = false,
                LastActiveAt = now,
                TrustScore = 0,
                MeetupCount = 0,
                CitiesCount = 0,
                CreatedAt = now,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.2.0.1");

        var response = await client.PatchAsJsonAsync(
            "api/v1/users/me",
            new { Bio = "New bio", IsOpenToRomance = true, HangoutTagIds = new[] { tag1Id, tag2Id } },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserDtoResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.Bio.Should().Be("New bio");
        body.IsOpenToRomance.Should().BeTrue();
        body.HangoutTagIds.Should().HaveCount(2);
        body.LastActiveAt.Should().Be(App.FakeTimeProvider.GetUtcNow());

        // Assert DB state
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var user = await db.Users
                .AsNoTracking()
                .Include(u => u.HangoutTags)
                .FirstOrDefaultAsync(u => u.Id == userId, TestContext.Current.CancellationToken);

            user.Should().NotBeNull();
            user!.Bio.Should().Be("New bio");
            user.IsOpenToRomance.Should().BeTrue();
            user.HangoutTags.Should().HaveCount(2);
            user.HangoutTags.Select(ht => ht.HangoutTagId).Should().BeEquivalentTo([tag1Id, tag2Id]);
        }
    }

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.2.0.2");

        var response = await client.PatchAsJsonAsync(
            "api/v1/users/me",
            new { Bio = "Test" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Valid token but no user row → 404 with User.NotRegistered.</summary>
    [Fact]
    public async Task HandleAsync_NoUserRow_Returns404()
    {
        const string SUB = "oid|updateme-no-user";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.2.0.3");

        var response = await client.PatchAsJsonAsync(
            "api/v1/users/me",
            new { Bio = "Test" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.User.NotRegistered);
    }

    /// <summary>Non-existent hangout tag ID → 400 with Validation.HangoutTagIdNotFound.</summary>
    [Fact]
    public async Task HandleAsync_NonExistentHangoutTagId_Returns400WithHangoutTagIdNotFound()
    {
        const string SUB = "oid|updateme-bad-tag";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var user = new User
            {
                Id = Guid.NewGuid(),
                AzureAdB2CId = SUB,
                FirstName = "Carol",
                LastActiveAt = now,
                TrustScore = 0,
                MeetupCount = 0,
                CitiesCount = 0,
                CreatedAt = now,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.2.0.4");

        var response = await client.PatchAsJsonAsync(
            "api/v1/users/me",
            new { HangoutTagIds = new[] { Guid.NewGuid() } },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.UserValidation.HangoutTagIdNotFound);
    }

    // Local DTO for deserialization
    private sealed record UserDtoResponse(
        Guid Id,
        string FirstName,
        string? Bio,
        bool IsIdVerified,
        bool IsOpenToday,
        bool IsOpenToRomance,
        DateTimeOffset LastActiveAt,
        int TrustScore,
        int MeetupCount,
        int CitiesCount,
        decimal? YearsNomading,
        Guid? CityId,
        DateTimeOffset CreatedAt,
        IReadOnlyList<Guid> HangoutTagIds);
}
