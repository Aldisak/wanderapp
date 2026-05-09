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

namespace WanderMeet.Api.IntegrationTests.Features.Meetups.SubmitReview;

/// <summary>Integration tests for POST /api/v1/meetups/{id}/review.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class SubmitReviewEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private static readonly Guid CoffeeTagId = new("00000000-0000-0000-0000-000000000CC1");

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
            Name = "City-SR-" + suffix,
            Country = "CZ",
            Location = CityCenter(),
            CreatedAt = now,
        });
        var placeId = Guid.NewGuid();
        db.Places.Add(new Place
        {
            Id = placeId,
            GooglePlaceId = "gsr-" + suffix,
            Name = "Place-SR-" + suffix,
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

    private static object AllPositiveReview(bool didMeet = true) => new
    {
        DidMeet = didMeet,
        FeltSafe = true,
        GoodConvo = true,
        WouldMeetAgain = true,
        Text = (string?)null,
    };

    // -----------------------------------------------------------------------
    // 401
    // -----------------------------------------------------------------------

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.2.1");

        var response = await client.PostAsJsonAsync(
            $"api/v1/meetups/{Guid.NewGuid()}/review",
            AllPositiveReview(),
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
        const string SUB = "oid|submit-no-user";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.2.2");

        var response = await client.PostAsJsonAsync(
            $"api/v1/meetups/{Guid.NewGuid()}/review",
            AllPositiveReview(),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.User.NotRegistered);
    }

    /// <summary>Unknown meetup id → 404 + Meetup.NotFound body.</summary>
    [Fact]
    public async Task HandleAsync_UnknownMeetupId_Returns404WithMeetupNotFound()
    {
        const string CALLER_SUB = "oid|submit-unknown-meetup";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.2.3");

        var response = await client.PostAsJsonAsync(
            $"api/v1/meetups/{Guid.NewGuid()}/review",
            AllPositiveReview(),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Caller is not a participant → 404 (not 403).</summary>
    [Fact]
    public async Task HandleAsync_CallerIsNotParticipant_Returns404WithMeetupNotFound()
    {
        const string CALLER_SUB = "oid|submit-not-participant-caller";
        const string USER_A_SUB = "oid|submit-not-participant-a";
        const string USER_B_SUB = "oid|submit-not-participant-b";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid meetupId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db);
            var caller = CreateUser(CALLER_SUB, now);
            var userA = CreateUser(USER_A_SUB, now);
            var userB = CreateUser(USER_B_SUB, now);
            db.Users.AddRange(caller, userA, userB);
            var (_, placeId) = SeedCityAndPlace(db, now, "submit-not-participant");
            var invite = MakeInvite(userA.Id, userB.Id, placeId, now);
            db.Invites.Add(invite);
            var meetup = MakeMeetup(invite, now.AddDays(-1));
            meetupId = meetup.Id;
            db.Meetups.Add(meetup);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.2.4");

        var response = await client.PostAsJsonAsync(
            $"api/v1/meetups/{meetupId}/review",
            AllPositiveReview(),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -----------------------------------------------------------------------
    // 409
    // -----------------------------------------------------------------------

    /// <summary>Caller already reviewed this meetup → 409 + Meetup.AlreadyReviewed.</summary>
    [Fact]
    public async Task HandleAsync_CallerAlreadyReviewedThisMeetup_Returns409WithMeetupAlreadyReviewed()
    {
        const string CALLER_SUB = "oid|submit-already-reviewed-caller";
        const string OTHER_SUB = "oid|submit-already-reviewed-other";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid meetupId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db);
            var caller = CreateUser(CALLER_SUB, now);
            var other = CreateUser(OTHER_SUB, now);
            db.Users.AddRange(caller, other);
            var (_, placeId) = SeedCityAndPlace(db, now, "submit-already-reviewed");
            var invite = MakeInvite(caller.Id, other.Id, placeId, now);
            db.Invites.Add(invite);
            var meetup = MakeMeetup(invite, now.AddDays(-1));
            meetupId = meetup.Id;
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
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.2.5");

        var response = await client.PostAsJsonAsync(
            $"api/v1/meetups/{meetupId}/review",
            AllPositiveReview(),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Meetup.AlreadyReviewed);
    }

    // -----------------------------------------------------------------------
    // 400
    // -----------------------------------------------------------------------

    /// <summary>Text over 120 chars → 400 + Validation.ReviewTextTooLong.</summary>
    [Fact]
    public async Task HandleAsync_TextOver120Chars_Returns400WithReviewTextTooLong()
    {
        const string CALLER_SUB = "oid|submit-text-too-long-caller";
        const string OTHER_SUB = "oid|submit-text-too-long-other";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid meetupId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db);
            var caller = CreateUser(CALLER_SUB, now);
            var other = CreateUser(OTHER_SUB, now);
            db.Users.AddRange(caller, other);
            var (_, placeId) = SeedCityAndPlace(db, now, "submit-text-too-long");
            var invite = MakeInvite(caller.Id, other.Id, placeId, now);
            db.Invites.Add(invite);
            var meetup = MakeMeetup(invite, now.AddDays(-1));
            meetupId = meetup.Id;
            db.Meetups.Add(meetup);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.2.6");

        var response = await client.PostAsJsonAsync(
            $"api/v1/meetups/{meetupId}/review",
            new { DidMeet = true, FeltSafe = true, GoodConvo = true, WouldMeetAgain = true, Text = new string('x', 121) },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Validation.ReviewTextTooLong);
    }

    // -----------------------------------------------------------------------
    // Happy-path tests
    // -----------------------------------------------------------------------

    /// <summary>Happy path: didMeet=true → review persisted, TrustScore=15, MeetupCount=1, place count incremented.</summary>
    [Fact]
    public async Task HandleAsync_HappyPathDidMeetTrue_PersistsReviewAndRecomputesTrustScoreAndMeetupCount()
    {
        const string CALLER_SUB = "oid|submit-happy-caller";
        const string OTHER_SUB = "oid|submit-happy-other";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid callerId;
        Guid otherId;
        Guid meetupId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db);
            var caller = CreateUser(CALLER_SUB, now);
            var other = CreateUser(OTHER_SUB, now);
            callerId = caller.Id;
            otherId = other.Id;
            db.Users.AddRange(caller, other);
            var (_, placeId) = SeedCityAndPlace(db, now, "submit-happy");
            var invite = MakeInvite(caller.Id, other.Id, placeId, now);
            db.Invites.Add(invite);
            var meetup = MakeMeetup(invite, now.AddDays(-1));
            meetupId = meetup.Id;
            db.Meetups.Add(meetup);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.2.7");

        var response = await client.PostAsJsonAsync(
            $"api/v1/meetups/{meetupId}/review",
            AllPositiveReview(),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubmitReviewResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.Review.Id.Should().NotBeEmpty();
        body.Review.MeetupId.Should().Be(meetupId);
        body.Review.ReviewerId.Should().Be(callerId);
        body.Review.RevieweeId.Should().Be(otherId);
        body.Review.DidMeet.Should().BeTrue();
        body.Reviewee.TrustScore.Should().Be(15);
        body.Reviewee.MeetupCount.Should().Be(1);

        // DB assertions
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var reviewRow = await db.MeetupReviews.AsNoTracking()
                .FirstOrDefaultAsync(r => r.MeetupId == meetupId && r.ReviewerId == callerId, TestContext.Current.CancellationToken);
            reviewRow.Should().NotBeNull();
            reviewRow!.RevieweeId.Should().Be(otherId);

            var reviewee = await db.Users.AsNoTracking()
                .FirstAsync(u => u.Id == otherId, TestContext.Current.CancellationToken);
            reviewee.TrustScore.Should().Be(15);
            reviewee.MeetupCount.Should().Be(1);
        }
    }

    /// <summary>Happy path: didMeet=true → place WanderMeetupCount incremented by 1.</summary>
    [Fact]
    public async Task HandleAsync_HappyPathDidMeetTrue_IncrementsPlaceWanderMeetupCountByOne()
    {
        const string CALLER_SUB = "oid|submit-place-incr-caller";
        const string OTHER_SUB = "oid|submit-place-incr-other";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid placeId;
        Guid meetupId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db);
            var caller = CreateUser(CALLER_SUB, now);
            var other = CreateUser(OTHER_SUB, now);
            db.Users.AddRange(caller, other);
            (_, placeId) = SeedCityAndPlace(db, now, "submit-place-incr");
            var invite = MakeInvite(caller.Id, other.Id, placeId, now);
            db.Invites.Add(invite);
            var meetup = MakeMeetup(invite, now.AddDays(-1));
            meetupId = meetup.Id;
            db.Meetups.Add(meetup);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Get baseline count
        int beforeCount;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var place = await db.Places.AsNoTracking()
                .FirstAsync(p => p.Id == placeId, TestContext.Current.CancellationToken);
            beforeCount = place.WanderMeetupCount;
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.2.8");

        var response = await client.PostAsJsonAsync(
            $"api/v1/meetups/{meetupId}/review",
            AllPositiveReview(didMeet: true),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var place = await db.Places.AsNoTracking()
                .FirstAsync(p => p.Id == placeId, TestContext.Current.CancellationToken);
            place.WanderMeetupCount.Should().Be(beforeCount + 1);
        }
    }

    /// <summary>didMeet=false → place WanderMeetupCount unchanged.</summary>
    [Fact]
    public async Task HandleAsync_DidMeetFalse_DoesNotIncrementPlaceWanderMeetupCount()
    {
        const string CALLER_SUB = "oid|submit-no-place-incr-caller";
        const string OTHER_SUB = "oid|submit-no-place-incr-other";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid placeId;
        Guid meetupId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db);
            var caller = CreateUser(CALLER_SUB, now);
            var other = CreateUser(OTHER_SUB, now);
            db.Users.AddRange(caller, other);
            (_, placeId) = SeedCityAndPlace(db, now, "submit-no-place-incr");
            var invite = MakeInvite(caller.Id, other.Id, placeId, now);
            db.Invites.Add(invite);
            var meetup = MakeMeetup(invite, now.AddDays(-1));
            meetupId = meetup.Id;
            db.Meetups.Add(meetup);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        int beforeCount;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var place = await db.Places.AsNoTracking()
                .FirstAsync(p => p.Id == placeId, TestContext.Current.CancellationToken);
            beforeCount = place.WanderMeetupCount;
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.2.9");

        var response = await client.PostAsJsonAsync(
            $"api/v1/meetups/{meetupId}/review",
            new { DidMeet = false, FeltSafe = true, GoodConvo = true, WouldMeetAgain = true, Text = (string?)null },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var place = await db.Places.AsNoTracking()
                .FirstAsync(p => p.Id == placeId, TestContext.Current.CancellationToken);
            place.WanderMeetupCount.Should().Be(beforeCount);
        }
    }

    /// <summary>50 prior all-positive did-meet reviews → 51st pushes TrustScore to 100 (clamped).</summary>
    [Fact]
    public async Task HandleAsync_TrustScoreClampedAt100_WhenSumExceedsCeiling()
    {
        const string CALLER_SUB = "oid|submit-clamp-caller";
        const string OTHER_SUB = "oid|submit-clamp-other";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid otherId;
        Guid meetupId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db);
            var caller = CreateUser(CALLER_SUB, now);
            var other = CreateUser(OTHER_SUB, now);
            otherId = other.Id;
            db.Users.AddRange(caller, other);
            var (_, placeId) = SeedCityAndPlace(db, now, "submit-clamp");

            // Seed 50 prior reviewers leaving all-positive did-meet reviews for 'other'
            for (var i = 0; i < 50; i++)
            {
                var priorReviewer = CreateUser("oid|submit-clamp-prior-" + i, now);
                db.Users.Add(priorReviewer);
                var priorInvite = MakeInvite(priorReviewer.Id, other.Id, placeId, now);
                db.Invites.Add(priorInvite);
                var priorMeetup = MakeMeetup(priorInvite, now.AddDays(-i - 2));
                db.Meetups.Add(priorMeetup);
                db.MeetupReviews.Add(new MeetupReview
                {
                    Id = Guid.NewGuid(),
                    MeetupId = priorMeetup.Id,
                    ReviewerId = priorReviewer.Id,
                    RevieweeId = other.Id,
                    DidMeet = true,
                    FeltSafe = true,
                    GoodConvo = true,
                    WouldMeetAgain = true,
                    CreatedAt = now.AddDays(-i - 1),
                });
            }

            // Meetup for caller → other
            var invite = MakeInvite(caller.Id, other.Id, placeId, now);
            db.Invites.Add(invite);
            var meetup = MakeMeetup(invite, now.AddDays(-1));
            meetupId = meetup.Id;
            db.Meetups.Add(meetup);

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.2.10");

        var response = await client.PostAsJsonAsync(
            $"api/v1/meetups/{meetupId}/review",
            AllPositiveReview(),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var reviewee = await db.Users.AsNoTracking()
                .FirstAsync(u => u.Id == otherId, TestContext.Current.CancellationToken);
            reviewee.TrustScore.Should().Be(100);
        }
    }

    /// <summary>Text=null → review persisted with null text.</summary>
    [Fact]
    public async Task HandleAsync_NullText_PersistsReviewWithNullText()
    {
        const string CALLER_SUB = "oid|submit-null-text-caller";
        const string OTHER_SUB = "oid|submit-null-text-other";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid callerId;
        Guid meetupId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db);
            var caller = CreateUser(CALLER_SUB, now);
            var other = CreateUser(OTHER_SUB, now);
            callerId = caller.Id;
            db.Users.AddRange(caller, other);
            var (_, placeId) = SeedCityAndPlace(db, now, "submit-null-text");
            var invite = MakeInvite(caller.Id, other.Id, placeId, now);
            db.Invites.Add(invite);
            var meetup = MakeMeetup(invite, now.AddDays(-1));
            meetupId = meetup.Id;
            db.Meetups.Add(meetup);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.2.11");

        var response = await client.PostAsJsonAsync(
            $"api/v1/meetups/{meetupId}/review",
            new { DidMeet = true, FeltSafe = true, GoodConvo = true, WouldMeetAgain = true, Text = (string?)null },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var reviewRow = await db.MeetupReviews.AsNoTracking()
                .FirstOrDefaultAsync(r => r.MeetupId == meetupId && r.ReviewerId == callerId, TestContext.Current.CancellationToken);
            reviewRow.Should().NotBeNull();
            reviewRow!.Text.Should().BeNull();
        }
    }

    /// <summary>Happy path: caller.LastActiveAt updated to FakeTimeProvider.GetUtcNow().</summary>
    [Fact]
    public async Task HandleAsync_HappyPath_UpdatesCallerLastActiveAt()
    {
        const string CALLER_SUB = "oid|submit-last-active-caller";
        const string OTHER_SUB = "oid|submit-last-active-other";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid callerId;
        Guid meetupId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db);
            var caller = CreateUser(CALLER_SUB, now.AddHours(-5));
            var other = CreateUser(OTHER_SUB, now);
            callerId = caller.Id;
            db.Users.AddRange(caller, other);
            var (_, placeId) = SeedCityAndPlace(db, now, "submit-last-active");
            var invite = MakeInvite(caller.Id, other.Id, placeId, now);
            db.Invites.Add(invite);
            var meetup = MakeMeetup(invite, now.AddDays(-1));
            meetupId = meetup.Id;
            db.Meetups.Add(meetup);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.2.12");

        var response = await client.PostAsJsonAsync(
            $"api/v1/meetups/{meetupId}/review",
            AllPositiveReview(),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var caller = await db.Users.AsNoTracking()
                .FirstAsync(u => u.Id == callerId, TestContext.Current.CancellationToken);
            caller.LastActiveAt.Should().Be(now);
        }
    }

    /// <summary>Response body reviewee object matches the recomputed trust score and meetup count.</summary>
    [Fact]
    public async Task HandleAsync_HappyPath_ReturnsRevieweeMiniWithRecomputedTrustScoreAndMeetupCount()
    {
        const string CALLER_SUB = "oid|submit-reviewee-stats-caller";
        const string OTHER_SUB = "oid|submit-reviewee-stats-other";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid otherId;
        Guid meetupId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db);
            var caller = CreateUser(CALLER_SUB, now);
            var other = CreateUser(OTHER_SUB, now);
            otherId = other.Id;
            db.Users.AddRange(caller, other);
            var (_, placeId) = SeedCityAndPlace(db, now, "submit-reviewee-stats");
            var invite = MakeInvite(caller.Id, other.Id, placeId, now);
            db.Invites.Add(invite);
            var meetup = MakeMeetup(invite, now.AddDays(-1));
            meetupId = meetup.Id;
            db.Meetups.Add(meetup);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.2.13");

        var response = await client.PostAsJsonAsync(
            $"api/v1/meetups/{meetupId}/review",
            AllPositiveReview(),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubmitReviewResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.Reviewee.Id.Should().Be(otherId);
        body.Reviewee.TrustScore.Should().Be(15);
        body.Reviewee.MeetupCount.Should().Be(1);
    }

    // -----------------------------------------------------------------------
    // Local DTOs for deserialization
    // -----------------------------------------------------------------------

    private sealed record SubmitReviewResponseDto(ReviewItemDto Review, RevieweeDto Reviewee);

    private sealed record ReviewItemDto(
        Guid Id,
        Guid MeetupId,
        Guid ReviewerId,
        Guid RevieweeId,
        bool DidMeet,
        bool FeltSafe,
        bool GoodConvo,
        bool WouldMeetAgain,
        string? Text,
        DateTimeOffset CreatedAt);

    private sealed record RevieweeDto(Guid Id, int TrustScore, int MeetupCount);
}
