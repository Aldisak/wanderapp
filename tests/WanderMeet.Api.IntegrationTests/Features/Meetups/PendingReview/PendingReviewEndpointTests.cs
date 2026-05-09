using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Meetups.PendingReview;

/// <summary>Integration tests for GET /api/v1/meetups/pending-review.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class PendingReviewEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private static readonly Guid CoffeeTagId = new("00000000-0000-0000-0000-000000000BB1");

    private static Point CityCenter() => new(14.42, 50.08) { SRID = 4326 };

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static User CreateUser(string sub, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        AzureAdB2CId = sub,
        FirstName = "User-" + sub,
        CreatedAt = now,
        LastActiveAt = now,
    };

    private static async Task EnsureCoffeeTagAsync(WanderMeetDbContext db)
    {
        if (!await db.HangoutTags.AnyAsync(h => h.Id == CoffeeTagId, TestContext.Current.CancellationToken))
        {
            db.HangoutTags.Add(new HangoutTag
            {
                Id = CoffeeTagId,
                Slug = HangoutTagSlug.Coffee,
                Label = "Coffee",
                Emoji = "☕",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
    }

    private static (Guid CityId, Guid PlaceId) SeedCityAndPlace(WanderMeetDbContext db, DateTimeOffset now, string suffix)
    {
        var cityId = Guid.NewGuid();
        db.Cities.Add(new City
        {
            Id = cityId,
            Name = "City-PR-" + suffix,
            Country = "CZ",
            Location = CityCenter(),
            CreatedAt = now,
        });
        var placeId = Guid.NewGuid();
        db.Places.Add(new Place
        {
            Id = placeId,
            GooglePlaceId = "gpr-" + suffix,
            Name = "Place-PR-" + suffix,
            CityId = cityId,
            Location = CityCenter(),
            Category = PlaceCategory.Cafe,
            CreatedAt = now,
        });
        return (cityId, placeId);
    }

    private static Invite MakeInvite(Guid senderId, Guid receiverId, Guid placeId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        SenderId = senderId,
        ReceiverId = receiverId,
        HangoutTagId = CoffeeTagId,
        PlaceId = placeId,
        SenderIsThere = false,
        Status = InviteStatus.Accepted,
        SentAt = now.AddHours(-2),
        RespondedAt = now.AddHours(-1),
        ExpiresAt = now.AddHours(46),
        CreatedAt = now.AddHours(-2),
    };

    private static Meetup MakeMeetup(Invite invite, DateTimeOffset metAt) => new()
    {
        Id = Guid.NewGuid(),
        InviteId = invite.Id,
        UserAId = invite.SenderId,
        UserBId = invite.ReceiverId,
        PlaceId = invite.PlaceId,
        MetAt = metAt,
        PromptSent = false,
        CreatedAt = metAt,
    };

    // -----------------------------------------------------------------------
    // 401
    // -----------------------------------------------------------------------

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.1.1");

        var response = await client.GetAsync(
            "api/v1/meetups/pending-review",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -----------------------------------------------------------------------
    // 404 tests
    // -----------------------------------------------------------------------

    /// <summary>JWT sub has no User row → 404 + User.NotRegistered.</summary>
    [Fact]
    public async Task HandleAsync_JwtSubHasNoUserRow_Returns404WithUserNotRegistered()
    {
        const string SUB = "oid|pending-no-user";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.1.2");

        var response = await client.GetAsync(
            "api/v1/meetups/pending-review",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.User.NotRegistered);
    }

    // -----------------------------------------------------------------------
    // Happy-path tests
    // -----------------------------------------------------------------------

    /// <summary>Happy path: only unreviewed meetups are returned, ordered by MetAt descending.</summary>
    [Fact]
    public async Task HandleAsync_HappyPath_ReturnsOnlyMeetupsCallerHasNotReviewedOrderedByMetAtDesc()
    {
        const string CALLER_SUB = "oid|pending-happy-caller";
        const string OTHER_SUB = "oid|pending-happy-other";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid reviewedMeetupId;
        Guid unreviewedMeetupId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db);
            var caller = CreateUser(CALLER_SUB, now);
            var other = CreateUser(OTHER_SUB, now);
            db.Users.AddRange(caller, other);
            var (_, placeId) = SeedCityAndPlace(db, now, "pending-happy");

            // Meetup 1: older, already reviewed by caller
            var invite1 = MakeInvite(other.Id, caller.Id, placeId, now);
            db.Invites.Add(invite1);
            var meetup1 = MakeMeetup(invite1, now.AddDays(-2));
            reviewedMeetupId = meetup1.Id;
            db.Meetups.Add(meetup1);
            db.MeetupReviews.Add(new MeetupReview
            {
                Id = Guid.NewGuid(),
                MeetupId = meetup1.Id,
                ReviewerId = caller.Id,
                RevieweeId = other.Id,
                DidMeet = true,
                FeltSafe = true,
                GoodConvo = true,
                WouldMeetAgain = true,
                CreatedAt = now.AddDays(-1),
            });

            // Meetup 2: newer, not reviewed by caller
            var invite2 = MakeInvite(caller.Id, other.Id, placeId, now);
            db.Invites.Add(invite2);
            var meetup2 = MakeMeetup(invite2, now.AddDays(-1));
            unreviewedMeetupId = meetup2.Id;
            db.Meetups.Add(meetup2);

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.1.3");

        var response = await client.GetAsync(
            "api/v1/meetups/pending-review",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PendingReviewResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(1);
        body.Items[0].MeetupId.Should().Be(unreviewedMeetupId);
    }

    /// <summary>When caller is UserA, OtherUser must be UserB.</summary>
    [Fact]
    public async Task HandleAsync_CallerIsUserA_OtherUserIsUserB()
    {
        const string CALLER_SUB = "oid|pending-userA-caller";
        const string OTHER_SUB = "oid|pending-userA-other";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid otherId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db);
            var caller = CreateUser(CALLER_SUB, now);
            var other = CreateUser(OTHER_SUB, now);
            otherId = other.Id;
            db.Users.AddRange(caller, other);
            var (_, placeId) = SeedCityAndPlace(db, now, "pending-userA");

            // caller is UserA (sender)
            var invite = MakeInvite(caller.Id, other.Id, placeId, now);
            db.Invites.Add(invite);
            db.Meetups.Add(MakeMeetup(invite, now.AddDays(-1)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.1.4");

        var response = await client.GetAsync(
            "api/v1/meetups/pending-review",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PendingReviewResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(1);
        body.Items[0].OtherUser.Id.Should().Be(otherId);
    }

    /// <summary>When caller is UserB, OtherUser must be UserA.</summary>
    [Fact]
    public async Task HandleAsync_CallerIsUserB_OtherUserIsUserA()
    {
        const string CALLER_SUB = "oid|pending-userB-caller";
        const string OTHER_SUB = "oid|pending-userB-other";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid otherId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db);
            var caller = CreateUser(CALLER_SUB, now);
            var other = CreateUser(OTHER_SUB, now);
            otherId = other.Id;
            db.Users.AddRange(caller, other);
            var (_, placeId) = SeedCityAndPlace(db, now, "pending-userB");

            // caller is UserB (receiver)
            var invite = MakeInvite(other.Id, caller.Id, placeId, now);
            db.Invites.Add(invite);
            db.Meetups.Add(MakeMeetup(invite, now.AddDays(-1)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.1.5");

        var response = await client.GetAsync(
            "api/v1/meetups/pending-review",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PendingReviewResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(1);
        body.Items[0].OtherUser.Id.Should().Be(otherId);
    }

    /// <summary>Already reviewed meetup must be excluded from the list.</summary>
    [Fact]
    public async Task HandleAsync_CallerHasAlreadyReviewed_ExcludesThatMeetup()
    {
        const string CALLER_SUB = "oid|pending-reviewed-caller";
        const string OTHER_SUB = "oid|pending-reviewed-other";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db);
            var caller = CreateUser(CALLER_SUB, now);
            var other = CreateUser(OTHER_SUB, now);
            db.Users.AddRange(caller, other);
            var (_, placeId) = SeedCityAndPlace(db, now, "pending-reviewed");

            var invite = MakeInvite(caller.Id, other.Id, placeId, now);
            db.Invites.Add(invite);
            var meetup = MakeMeetup(invite, now.AddDays(-1));
            db.Meetups.Add(meetup);
            db.MeetupReviews.Add(new MeetupReview
            {
                Id = Guid.NewGuid(),
                MeetupId = meetup.Id,
                ReviewerId = caller.Id,
                RevieweeId = other.Id,
                DidMeet = true,
                CreatedAt = now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.1.6");

        var response = await client.GetAsync(
            "api/v1/meetups/pending-review",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PendingReviewResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
    }

    /// <summary>More than 50 unreviewed meetups → capped at 50.</summary>
    [Fact]
    public async Task HandleAsync_CapsAt50Items()
    {
        const string CALLER_SUB = "oid|pending-cap-caller";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db);
            var caller = CreateUser(CALLER_SUB, now);
            db.Users.Add(caller);
            var (_, placeId) = SeedCityAndPlace(db, now, "pending-cap");

            for (var i = 0; i < 51; i++)
            {
                var other = CreateUser("oid|pending-cap-other-" + i, now);
                db.Users.Add(other);
                var invite = MakeInvite(caller.Id, other.Id, placeId, now);
                db.Invites.Add(invite);
                db.Meetups.Add(MakeMeetup(invite, now.AddDays(-i - 1)));
            }

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.1.7");

        var response = await client.GetAsync(
            "api/v1/meetups/pending-review",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PendingReviewResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(50);
    }

    // -----------------------------------------------------------------------
    // Local DTOs for deserialization
    // -----------------------------------------------------------------------

    private sealed record PendingReviewResponseDto(List<PendingMeetupItemDto> Items);

    private sealed record PendingMeetupItemDto(
        Guid MeetupId,
        MeetupUserDto OtherUser,
        MeetupPlaceDto Place,
        DateTimeOffset MetAt);

    private sealed record MeetupUserDto(Guid Id, string FirstName, string? PhotoUrl);
    private sealed record MeetupPlaceDto(Guid Id, string Name, PlaceCategory Category);
}
