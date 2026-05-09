using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Users.PublicReviews;

/// <summary>Integration tests for GET /api/v1/users/{id}/reviews.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class ListPublicReviewsEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private static readonly Guid HangoutTagId = new("00000000-0000-0000-0000-000000000CC1");

    // -----------------------------------------------------------------------
    // Happy path: target has mixed DidMeet reviews — only DidMeet=true returned
    // -----------------------------------------------------------------------

    /// <summary>Only DidMeet=true reviews are returned; DidMeet=false reviews are excluded.</summary>
    [Fact]
    public async Task HandleAsync_TargetHasMixedDidMeet_ReturnsOnlyDidMeetTrueItems()
    {
        const string CALLER_SUB = "oid|pr-mixed-caller";
        const string REVIEWER_SUB = "oid|pr-mixed-reviewer";
        const string TARGET_SUB = "oid|pr-mixed-target";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid targetId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureHangoutTagAsync(db);

            var caller = MakeUser(CALLER_SUB, now);
            var reviewer = MakeUser(REVIEWER_SUB, now);
            var target = MakeUser(TARGET_SUB, now);
            targetId = target.Id;
            db.Users.AddRange(caller, reviewer, target);

            var (_, placeId) = SeedCityAndPlace(db, now, "pr-mixed");

            // DidMeet=true review (should appear)
            var invite1 = MakeInvite(reviewer.Id, target.Id, placeId, now);
            var meetup1 = MakeMeetup(invite1, now.AddDays(-2));
            db.Invites.Add(invite1);
            db.Meetups.Add(meetup1);
            db.MeetupReviews.Add(MakeReview(reviewer.Id, target.Id, meetup1.Id, didMeet: true, now.AddDays(-2)));

            // DidMeet=false review (must be excluded)
            var invite2 = MakeInvite(reviewer.Id, target.Id, placeId, now.AddDays(-1));
            var meetup2 = MakeMeetup(invite2, now.AddDays(-1));
            db.Invites.Add(invite2);
            db.Meetups.Add(meetup2);
            db.MeetupReviews.Add(MakeReview(reviewer.Id, target.Id, meetup2.Id, didMeet: false, now.AddDays(-1)));

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.1");

        var response = await client.GetAsync($"api/v1/users/{targetId}/reviews", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListPublicReviewsResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(1);
        body.Items[0].FeltSafe.Should().BeTrue();
    }

    /// <summary>Three reviews returned in CreatedAt DESC order.</summary>
    [Fact]
    public async Task HandleAsync_MultipleReviews_OrdersByCreatedAtDescending()
    {
        const string CALLER_SUB = "oid|pr-order-caller";
        const string TARGET_SUB = "oid|pr-order-target";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid targetId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureHangoutTagAsync(db);

            var caller = MakeUser(CALLER_SUB, now);
            var target = MakeUser(TARGET_SUB, now);
            targetId = target.Id;
            var reviewer1 = MakeUser("oid|pr-order-rev1", now);
            var reviewer2 = MakeUser("oid|pr-order-rev2", now);
            var reviewer3 = MakeUser("oid|pr-order-rev3", now);
            db.Users.AddRange(caller, target, reviewer1, reviewer2, reviewer3);

            var (_, placeId) = SeedCityAndPlace(db, now, "pr-order");

            var inv1 = MakeInvite(reviewer1.Id, target.Id, placeId, now);
            var meetup1 = MakeMeetup(inv1, now.AddDays(-3));
            db.Invites.Add(inv1);
            db.Meetups.Add(meetup1);
            db.MeetupReviews.Add(MakeReview(reviewer1.Id, target.Id, meetup1.Id, didMeet: true, now.AddDays(-3)));

            var inv2 = MakeInvite(reviewer2.Id, target.Id, placeId, now.AddDays(-1));
            var meetup2 = MakeMeetup(inv2, now.AddDays(-2));
            db.Invites.Add(inv2);
            db.Meetups.Add(meetup2);
            db.MeetupReviews.Add(MakeReview(reviewer2.Id, target.Id, meetup2.Id, didMeet: true, now.AddDays(-2)));

            var inv3 = MakeInvite(reviewer3.Id, target.Id, placeId, now.AddDays(-2));
            var meetup3 = MakeMeetup(inv3, now.AddDays(-1));
            db.Invites.Add(inv3);
            db.Meetups.Add(meetup3);
            db.MeetupReviews.Add(MakeReview(reviewer3.Id, target.Id, meetup3.Id, didMeet: true, now.AddDays(-1)));

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.2");

        var response = await client.GetAsync($"api/v1/users/{targetId}/reviews", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListPublicReviewsResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(3);
        body.Items[0].CreatedAt.Should().BeAfter(body.Items[1].CreatedAt);
        body.Items[1].CreatedAt.Should().BeAfter(body.Items[2].CreatedAt);
    }

    /// <summary>Target user with zero reviews → 200 with empty items list.</summary>
    [Fact]
    public async Task HandleAsync_TargetHasNoReviews_Returns200WithEmptyItems()
    {
        const string CALLER_SUB = "oid|pr-empty-caller";
        const string TARGET_SUB = "oid|pr-empty-target";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid targetId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var caller = MakeUser(CALLER_SUB, now);
            var target = MakeUser(TARGET_SUB, now);
            targetId = target.Id;
            db.Users.AddRange(caller, target);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.3");

        var response = await client.GetAsync($"api/v1/users/{targetId}/reviews", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListPublicReviewsResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
    }

    /// <summary>All reviews have DidMeet=false → 200 with empty items (NOT 404).</summary>
    [Fact]
    public async Task HandleAsync_AllReviewsDidMeetFalse_Returns200WithEmptyItems()
    {
        const string CALLER_SUB = "oid|pr-allfalse-caller";
        const string TARGET_SUB = "oid|pr-allfalse-target";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid targetId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureHangoutTagAsync(db);

            var caller = MakeUser(CALLER_SUB, now);
            var target = MakeUser(TARGET_SUB, now);
            targetId = target.Id;
            var reviewer = MakeUser("oid|pr-allfalse-rev", now);
            db.Users.AddRange(caller, target, reviewer);

            var (_, placeId) = SeedCityAndPlace(db, now, "pr-allfalse");
            var inv = MakeInvite(reviewer.Id, target.Id, placeId, now);
            var meetup = MakeMeetup(inv, now.AddDays(-1));
            db.Invites.Add(inv);
            db.Meetups.Add(meetup);
            db.MeetupReviews.Add(MakeReview(reviewer.Id, target.Id, meetup.Id, didMeet: false, now.AddDays(-1)));

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.4");

        var response = await client.GetAsync($"api/v1/users/{targetId}/reviews", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListPublicReviewsResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
    }

    /// <summary>51 DidMeet=true reviews → only 50 returned; the oldest one is excluded.</summary>
    [Fact]
    public async Task HandleAsync_MoreThan50Reviews_CapsAt50()
    {
        const string CALLER_SUB = "oid|pr-cap-caller";
        const string TARGET_SUB = "oid|pr-cap-target";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid targetId;
        DateTimeOffset oldestCreatedAt;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureHangoutTagAsync(db);

            var caller = MakeUser(CALLER_SUB, now);
            var target = MakeUser(TARGET_SUB, now);
            targetId = target.Id;
            db.Users.AddRange(caller, target);

            var (_, placeId) = SeedCityAndPlace(db, now, "pr-cap");

            oldestCreatedAt = now.AddDays(-51);
            for (var i = 0; i < 51; i++)
            {
                var reviewer = MakeUser($"oid|pr-cap-rev-{i}", now);
                db.Users.Add(reviewer);
                var inv = MakeInvite(reviewer.Id, target.Id, placeId, now.AddDays(-i - 1));
                var meetup = MakeMeetup(inv, now.AddDays(-i - 1));
                db.Invites.Add(inv);
                db.Meetups.Add(meetup);
                db.MeetupReviews.Add(MakeReview(reviewer.Id, target.Id, meetup.Id, didMeet: true, now.AddDays(-i - 1)));
            }

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.5");

        var response = await client.GetAsync($"api/v1/users/{targetId}/reviews", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListPublicReviewsResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(50);
        body.Items.Should().NotContain(item => item.CreatedAt == oldestCreatedAt);
    }

    /// <summary>Reviewer with DeletedAt != null still appears in the public reviews list.</summary>
    [Fact]
    public async Task HandleAsync_ReviewerSoftDeleted_StillIncludedInItems()
    {
        const string CALLER_SUB = "oid|pr-revdel-caller";
        const string TARGET_SUB = "oid|pr-revdel-target";
        const string REVIEWER_SUB = "oid|pr-revdel-rev";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid targetId;
        Guid reviewerId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureHangoutTagAsync(db);

            var caller = MakeUser(CALLER_SUB, now);
            var target = MakeUser(TARGET_SUB, now);
            targetId = target.Id;
            var reviewer = MakeUser(REVIEWER_SUB, now);
            reviewerId = reviewer.Id;
            reviewer.DeletedAt = now.AddDays(-1);
            db.Users.AddRange(caller, target, reviewer);

            var (_, placeId) = SeedCityAndPlace(db, now, "pr-revdel");
            var inv = MakeInvite(reviewer.Id, target.Id, placeId, now.AddDays(-2));
            var meetup = MakeMeetup(inv, now.AddDays(-2));
            db.Invites.Add(inv);
            db.Meetups.Add(meetup);
            db.MeetupReviews.Add(MakeReview(reviewer.Id, target.Id, meetup.Id, didMeet: true, now.AddDays(-2)));

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.6");

        var response = await client.GetAsync($"api/v1/users/{targetId}/reviews", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListPublicReviewsResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(1);
        body.Items[0].Reviewer.Id.Should().Be(reviewerId);
        body.Items[0].Reviewer.FirstName.Should().NotBeNullOrEmpty();
    }

    /// <summary>Reviewer with photos at Order=0 deleted and Order=1 active → PhotoUrl == Order=1 BlobUrl.</summary>
    [Fact]
    public async Task HandleAsync_ReviewerHasMultiplePhotos_ProjectsLowestOrderNonDeleted()
    {
        const string CALLER_SUB = "oid|pr-photos-caller";
        const string TARGET_SUB = "oid|pr-photos-target";
        const string REVIEWER_SUB = "oid|pr-photos-rev";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid targetId;
        const string expectedUrl = "https://cdn.example.com/order1.jpg";

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureHangoutTagAsync(db);

            var caller = MakeUser(CALLER_SUB, now);
            var target = MakeUser(TARGET_SUB, now);
            targetId = target.Id;
            var reviewer = MakeUser(REVIEWER_SUB, now);
            db.Users.AddRange(caller, target, reviewer);

            // Order=0 deleted, Order=1 active, Order=2 active
            db.UserPhotos.Add(new UserPhoto { Id = Guid.NewGuid(), UserId = reviewer.Id, BlobUrl = "https://cdn.example.com/order0.jpg", Order = 0, CreatedAt = now, DeletedAt = now.AddMinutes(-1) });
            db.UserPhotos.Add(new UserPhoto { Id = Guid.NewGuid(), UserId = reviewer.Id, BlobUrl = expectedUrl, Order = 1, CreatedAt = now });
            db.UserPhotos.Add(new UserPhoto { Id = Guid.NewGuid(), UserId = reviewer.Id, BlobUrl = "https://cdn.example.com/order2.jpg", Order = 2, CreatedAt = now });

            var (_, placeId) = SeedCityAndPlace(db, now, "pr-photos");
            var inv = MakeInvite(reviewer.Id, target.Id, placeId, now.AddDays(-1));
            var meetup = MakeMeetup(inv, now.AddDays(-1));
            db.Invites.Add(inv);
            db.Meetups.Add(meetup);
            db.MeetupReviews.Add(MakeReview(reviewer.Id, target.Id, meetup.Id, didMeet: true, now.AddDays(-1)));

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.7");

        var response = await client.GetAsync($"api/v1/users/{targetId}/reviews", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListPublicReviewsResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(1);
        body.Items[0].Reviewer.PhotoUrl.Should().Be(expectedUrl);
    }

    /// <summary>Reviewer with no photos → PhotoUrl is null.</summary>
    [Fact]
    public async Task HandleAsync_ReviewerHasNoPhotos_PhotoUrlIsNull()
    {
        const string CALLER_SUB = "oid|pr-nophoto-caller";
        const string TARGET_SUB = "oid|pr-nophoto-target";
        const string REVIEWER_SUB = "oid|pr-nophoto-rev";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid targetId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureHangoutTagAsync(db);

            var caller = MakeUser(CALLER_SUB, now);
            var target = MakeUser(TARGET_SUB, now);
            targetId = target.Id;
            var reviewer = MakeUser(REVIEWER_SUB, now);
            db.Users.AddRange(caller, target, reviewer);

            var (_, placeId) = SeedCityAndPlace(db, now, "pr-nophoto");
            var inv = MakeInvite(reviewer.Id, target.Id, placeId, now.AddDays(-1));
            var meetup = MakeMeetup(inv, now.AddDays(-1));
            db.Invites.Add(inv);
            db.Meetups.Add(meetup);
            db.MeetupReviews.Add(MakeReview(reviewer.Id, target.Id, meetup.Id, didMeet: true, now.AddDays(-1)));

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.8");

        var response = await client.GetAsync($"api/v1/users/{targetId}/reviews", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListPublicReviewsResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(1);
        body.Items[0].Reviewer.PhotoUrl.Should().BeNull();
    }

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.9");

        var response = await client.GetAsync($"api/v1/users/{Guid.NewGuid()}/reviews", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Route id does not match any User row → 404.</summary>
    [Fact]
    public async Task HandleAsync_UnknownUserId_Returns404()
    {
        const string CALLER_SUB = "oid|pr-unkn-caller";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var caller = MakeUser(CALLER_SUB, now);
            db.Users.Add(caller);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.10");

        var response = await client.GetAsync($"api/v1/users/{Guid.NewGuid()}/reviews", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Target user is soft-deleted → 404.</summary>
    [Fact]
    public async Task HandleAsync_TargetSoftDeleted_Returns404()
    {
        const string CALLER_SUB = "oid|pr-softdel-caller";
        const string TARGET_SUB = "oid|pr-softdel-target";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid targetId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var caller = MakeUser(CALLER_SUB, now);
            var target = MakeUser(TARGET_SUB, now);
            targetId = target.Id;
            target.DeletedAt = now.AddDays(-1);
            db.Users.AddRange(caller, target);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.11");

        var response = await client.GetAsync($"api/v1/users/{targetId}/reviews", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Caller views own profile → 200 with the same response shape.</summary>
    [Fact]
    public async Task HandleAsync_CallerViewsOwnProfile_Returns200WithSameShape()
    {
        const string CALLER_SUB = "oid|pr-own-caller";
        const string REVIEWER_SUB = "oid|pr-own-reviewer";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid callerId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureHangoutTagAsync(db);

            var caller = MakeUser(CALLER_SUB, now);
            callerId = caller.Id;
            var reviewer = MakeUser(REVIEWER_SUB, now);
            db.Users.AddRange(caller, reviewer);

            var (_, placeId) = SeedCityAndPlace(db, now, "pr-own");
            var inv = MakeInvite(reviewer.Id, caller.Id, placeId, now.AddDays(-1));
            var meetup = MakeMeetup(inv, now.AddDays(-1));
            db.Invites.Add(inv);
            db.Meetups.Add(meetup);
            db.MeetupReviews.Add(MakeReview(reviewer.Id, caller.Id, meetup.Id, didMeet: true, now.AddDays(-1)));

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.12");

        var response = await client.GetAsync($"api/v1/users/{callerId}/reviews", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListPublicReviewsResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(1);
        body.Items[0].Reviewer.Should().NotBeNull();
        body.Items[0].Reviewer.Id.Should().NotBeEmpty();
        body.Items[0].Reviewer.FirstName.Should().NotBeNullOrEmpty();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static User MakeUser(string sub, DateTimeOffset now, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        AzureAdB2CId = sub,
        FirstName = "User-" + sub[^6..],
        CreatedAt = now,
        LastActiveAt = now,
    };

    private static async Task EnsureHangoutTagAsync(WanderMeetDbContext db)
    {
        if (!await db.HangoutTags.AnyAsync(h => h.Id == HangoutTagId, TestContext.Current.CancellationToken))
        {
            db.HangoutTags.Add(new HangoutTag
            {
                Id = HangoutTagId,
                Slug = HangoutTagSlug.Coffee,
                Label = "Coffee-PR",
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
            Location = new Point(14.42, 50.08) { SRID = 4326 },
            CreatedAt = now,
        });
        var placeId = Guid.NewGuid();
        db.Places.Add(new Place
        {
            Id = placeId,
            GooglePlaceId = "gpr-" + suffix,
            Name = "Place-PR-" + suffix,
            CityId = cityId,
            Location = new Point(14.42, 50.08) { SRID = 4326 },
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
        HangoutTagId = HangoutTagId,
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

    private static MeetupReview MakeReview(Guid reviewerId, Guid revieweeId, Guid meetupId, bool didMeet, DateTimeOffset createdAt) => new()
    {
        Id = Guid.NewGuid(),
        MeetupId = meetupId,
        ReviewerId = reviewerId,
        RevieweeId = revieweeId,
        DidMeet = didMeet,
        FeltSafe = didMeet,
        GoodConvo = didMeet,
        WouldMeetAgain = didMeet,
        Text = didMeet ? "Great!" : null,
        CreatedAt = createdAt,
    };

    // -----------------------------------------------------------------------
    // Local DTOs for deserialization
    // -----------------------------------------------------------------------

    private sealed record ListPublicReviewsResponseDto(List<PublicReviewItemDto> Items);

    private sealed record PublicReviewItemDto(
        Guid Id,
        ReviewerMiniItemDto Reviewer,
        bool FeltSafe,
        bool GoodConvo,
        bool WouldMeetAgain,
        string? Text,
        DateTimeOffset CreatedAt);

    private sealed record ReviewerMiniItemDto(Guid Id, string FirstName, string? PhotoUrl);
}
