using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.Infrastructure.Jobs;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Infrastructure.Jobs;

/// <summary>Integration tests for <see cref="ReviewPromptJob"/>.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class ReviewPromptJobTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private static Point CityCenter() => new(14.42, 50.08) { SRID = 4326 };

    private async Task<(Guid cityId, Guid placeId, Guid tagId)> SeedMinimalAsync(DateTimeOffset now)
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();

        var city = new City
        {
            Id = Guid.NewGuid(),
            Name = "Review City",
            Country = "CZ",
            Location = CityCenter(),
            CreatedAt = now,
        };
        db.Cities.Add(city);

        var tag = new HangoutTag
        {
            Id = Guid.NewGuid(),
            Slug = HangoutTagSlug.Coffee,
            Label = "Coffee",
            Emoji = "☕",
            CreatedAt = now,
        };
        db.HangoutTags.Add(tag);

        var place = new Place
        {
            Id = Guid.NewGuid(),
            GooglePlaceId = $"gp_{Guid.NewGuid()}",
            Name = "Review Place",
            CityId = city.Id,
            Location = CityCenter(),
            Category = PlaceCategory.Cafe,
            CreatedAt = now,
        };
        db.Places.Add(place);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (city.Id, place.Id, tag.Id);
    }

    private static User MakeUser(string sub, Guid cityId, DateTimeOffset now, string? fcmToken) => new()
    {
        Id = Guid.NewGuid(),
        AzureAdB2CId = sub,
        FirstName = sub.Length > 8 ? sub[..8] : sub,
        CityId = cityId,
        CreatedAt = now,
        LastActiveAt = now,
        FcmToken = fcmToken,
    };

    private static Invite MakeInvite(Guid senderId, Guid receiverId, Guid tagId, Guid placeId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        SenderId = senderId,
        ReceiverId = receiverId,
        HangoutTagId = tagId,
        PlaceId = placeId,
        Status = InviteStatus.Accepted,
        SentAt = now,
        ExpiresAt = now + ValidationConstants.InviteExpiryWindow,
        RespondedAt = now,
        CreatedAt = now,
    };

    private static Meetup MakeMeetup(Guid inviteId, Guid userAId, Guid userBId, Guid placeId,
        DateTimeOffset metAt, DateTimeOffset now, bool promptSent = false) => new()
    {
        Id = Guid.NewGuid(),
        InviteId = inviteId,
        UserAId = userAId,
        UserBId = userBId,
        PlaceId = placeId,
        MetAt = metAt,
        PromptSent = promptSent,
        CreatedAt = now,
    };

    /// <summary>Meetup over 3h with PromptSent=false → fires FCM pushes to both participants and sets PromptSent=true.</summary>
    [Fact]
    public async Task ReviewPromptJob_ExecuteAsync_MeetupOver3hAndPromptNotSent_FiresFcmPushAndSetsPromptSent()
    {
        var now = App.FakeTimeProvider.GetUtcNow();
        var (cityId, placeId, tagId) = await SeedMinimalAsync(now);
        var ct = TestContext.Current.CancellationToken;

        const string TOKEN_A = "fcm-token-userA-over3h";
        const string TOKEN_B = "fcm-token-userB-over3h";
        Guid meetupId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var userA = MakeUser("rp-usrA-01", cityId, now, TOKEN_A);
            var userB = MakeUser("rp-usrB-01", cityId, now, TOKEN_B);
            db.Users.AddRange(userA, userB);
            await db.SaveChangesAsync(ct);

            var invite = MakeInvite(userA.Id, userB.Id, tagId, placeId, now);
            db.Invites.Add(invite);

            // MetAt = now - 3h - 1min (just past the threshold)
            var meetup = MakeMeetup(invite.Id, userA.Id, userB.Id, placeId,
                metAt: now - ValidationConstants.ReviewPromptDelay - TimeSpan.FromMinutes(1),
                now: now);
            db.Meetups.Add(meetup);
            await db.SaveChangesAsync(ct);
            meetupId = meetup.Id;
        }

        using var jobScope = App.Services.CreateScope();
        var job = new ReviewPromptJob(
            jobScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>(),
            App.FcmClient,
            jobScope.ServiceProvider.GetRequiredService<TimeProvider>(),
            jobScope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ReviewPromptJob>>());
        await job.ExecuteAsync(ct);

        // 2 FCM sends (one per participant)
        App.FcmClient.Sends.Should().HaveCount(2);
        App.FcmClient.Sends.Should().AllSatisfy(s => s.Title.Should().Be("How did it go?"));
        // UserA receives body with UserB's first name
        App.FcmClient.Sends.Should().Contain(s => s.Token == TOKEN_A && s.Body.Contains("rp-usrB-"));
        // UserB receives body with UserA's first name
        App.FcmClient.Sends.Should().Contain(s => s.Token == TOKEN_B && s.Body.Contains("rp-usrA-"));

        // PromptSent flipped
        using var assertScope = App.Services.CreateScope();
        var db2 = assertScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var meetup2 = await db2.Meetups.AsNoTracking().FirstAsync(m => m.Id == meetupId, ct);
        meetup2.PromptSent.Should().BeTrue();
    }

    /// <summary>Both participants have no FCM token → no sends, but PromptSent still set to true.</summary>
    [Fact]
    public async Task ReviewPromptJob_ExecuteAsync_BothParticipantsHaveNoFcmToken_StillSetsPromptSentTrue()
    {
        var now = App.FakeTimeProvider.GetUtcNow();
        var (cityId, placeId, tagId) = await SeedMinimalAsync(now);
        var ct = TestContext.Current.CancellationToken;

        Guid meetupId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var userA = MakeUser("rp-usrA-notoken", cityId, now, null);
            var userB = MakeUser("rp-usrB-notoken", cityId, now, null);
            db.Users.AddRange(userA, userB);
            await db.SaveChangesAsync(ct);

            var invite = MakeInvite(userA.Id, userB.Id, tagId, placeId, now);
            db.Invites.Add(invite);
            var meetup = MakeMeetup(invite.Id, userA.Id, userB.Id, placeId,
                metAt: now - ValidationConstants.ReviewPromptDelay - TimeSpan.FromMinutes(5),
                now: now);
            db.Meetups.Add(meetup);
            await db.SaveChangesAsync(ct);
            meetupId = meetup.Id;
        }

        using var jobScope = App.Services.CreateScope();
        var job = new ReviewPromptJob(
            jobScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>(),
            App.FcmClient,
            jobScope.ServiceProvider.GetRequiredService<TimeProvider>(),
            jobScope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ReviewPromptJob>>());
        await job.ExecuteAsync(ct);

        App.FcmClient.Sends.Should().BeEmpty("both users have no FCM token");

        using var assertScope = App.Services.CreateScope();
        var db2 = assertScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var meetup2 = await db2.Meetups.AsNoTracking().FirstAsync(m => m.Id == meetupId, ct);
        meetup2.PromptSent.Should().BeTrue("DB flip is idempotency anchor");
    }

    /// <summary>FCM throws → PromptSent still flipped to true; no exception escapes the job.</summary>
    [Fact]
    public async Task ReviewPromptJob_ExecuteAsync_FcmThrows_PromptSentStillFlippedTrue()
    {
        var now = App.FakeTimeProvider.GetUtcNow();
        var (cityId, placeId, tagId) = await SeedMinimalAsync(now);
        var ct = TestContext.Current.CancellationToken;

        Guid meetupId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var userA = MakeUser("rp-usrA-fcmthrow", cityId, now, "token-a-fcmthrow");
            var userB = MakeUser("rp-usrB-fcmthrow", cityId, now, "token-b-fcmthrow");
            db.Users.AddRange(userA, userB);
            await db.SaveChangesAsync(ct);

            var invite = MakeInvite(userA.Id, userB.Id, tagId, placeId, now);
            db.Invites.Add(invite);
            var meetup = MakeMeetup(invite.Id, userA.Id, userB.Id, placeId,
                metAt: now - ValidationConstants.ReviewPromptDelay - TimeSpan.FromMinutes(5),
                now: now);
            db.Meetups.Add(meetup);
            await db.SaveChangesAsync(ct);
            meetupId = meetup.Id;
        }

        App.FcmClient.ThrowOnSend = new InvalidOperationException("FCM down");

        using var jobScope = App.Services.CreateScope();
        var job = new ReviewPromptJob(
            jobScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>(),
            App.FcmClient,
            jobScope.ServiceProvider.GetRequiredService<TimeProvider>(),
            jobScope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ReviewPromptJob>>());

        // Must not throw even when FCM fails
        var act = async () => await job.ExecuteAsync(ct);
        await act.Should().NotThrowAsync();

        using var assertScope = App.Services.CreateScope();
        var db2 = assertScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var meetup2 = await db2.Meetups.AsNoTracking().FirstAsync(m => m.Id == meetupId, ct);
        meetup2.PromptSent.Should().BeTrue("DB flip is the idempotency anchor regardless of FCM outcome");
    }

    /// <summary>Meetup under 3h → not prompted yet, PromptSent stays false.</summary>
    [Fact]
    public async Task ReviewPromptJob_ExecuteAsync_MeetupUnder3h_LeftAlone()
    {
        var now = App.FakeTimeProvider.GetUtcNow();
        var (cityId, placeId, tagId) = await SeedMinimalAsync(now);
        var ct = TestContext.Current.CancellationToken;

        Guid meetupId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var userA = MakeUser("rp-usrA-under3h", cityId, now, "token-a-under3h");
            var userB = MakeUser("rp-usrB-under3h", cityId, now, "token-b-under3h");
            db.Users.AddRange(userA, userB);
            await db.SaveChangesAsync(ct);

            var invite = MakeInvite(userA.Id, userB.Id, tagId, placeId, now);
            db.Invites.Add(invite);
            // MetAt = now - 1h (well under 3h threshold)
            var meetup = MakeMeetup(invite.Id, userA.Id, userB.Id, placeId,
                metAt: now - TimeSpan.FromHours(1),
                now: now);
            db.Meetups.Add(meetup);
            await db.SaveChangesAsync(ct);
            meetupId = meetup.Id;
        }

        using var jobScope = App.Services.CreateScope();
        var job = new ReviewPromptJob(
            jobScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>(),
            App.FcmClient,
            jobScope.ServiceProvider.GetRequiredService<TimeProvider>(),
            jobScope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ReviewPromptJob>>());
        await job.ExecuteAsync(ct);

        App.FcmClient.Sends.Should().BeEmpty("meetup is under 3h — not yet eligible");

        using var assertScope = App.Services.CreateScope();
        var db2 = assertScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var meetup2 = await db2.Meetups.AsNoTracking().FirstAsync(m => m.Id == meetupId, ct);
        meetup2.PromptSent.Should().BeFalse("meetup under threshold should not be touched");
    }
}
