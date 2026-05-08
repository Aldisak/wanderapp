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

namespace WanderMeet.Api.IntegrationTests.Features.Invites.AcceptInvite;

/// <summary>Integration tests for PATCH /api/v1/invites/{id}/accept.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class AcceptInviteEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private static readonly Guid CoffeeTagId = new("00000000-0000-0000-0000-0000000000A1");

    private static Point CityCenter() => new(14.42, 50.08) { SRID = 4326 };

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static User CreateUser(string sub, Guid? cityId, DateTimeOffset now, bool deleted = false) => new()
    {
        Id = Guid.NewGuid(),
        AzureAdB2CId = sub,
        FirstName = "User-" + sub,
        CreatedAt = now,
        LastActiveAt = now,
        CityId = cityId,
        DeletedAt = deleted ? now : null,
    };

    private static async Task EnsureCoffeeTagAsync(WanderMeetDbContext db, DateTimeOffset now)
    {
        if (!await db.HangoutTags.AnyAsync(h => h.Id == CoffeeTagId, TestContext.Current.CancellationToken))
        {
            db.HangoutTags.Add(new HangoutTag
            {
                Id = CoffeeTagId,
                Slug = HangoutTagSlug.Coffee,
                Label = "Coffee",
                Emoji = "☕",
                CreatedAt = now,
            });
        }
    }

    private static (Guid CityId, Guid PlaceId) SeedCityAndPlace(WanderMeetDbContext db, DateTimeOffset now, string suffix)
    {
        var cityId = Guid.NewGuid();
        db.Cities.Add(new City
        {
            Id = cityId,
            Name = "City-" + suffix,
            Country = "CZ",
            Location = CityCenter(),
            CreatedAt = now,
        });
        var placeId = Guid.NewGuid();
        db.Places.Add(new Place
        {
            Id = placeId,
            GooglePlaceId = "g-" + suffix,
            Name = "Place-" + suffix,
            CityId = cityId,
            Location = CityCenter(),
            Category = PlaceCategory.Cafe,
            CreatedAt = now,
        });
        return (cityId, placeId);
    }

    private static Invite MakeInvite(Guid senderId, Guid receiverId, Guid placeId, DateTimeOffset now,
        InviteStatus status = InviteStatus.Pending, DateTimeOffset? respondedAt = null, DateTimeOffset? expiresAt = null) => new()
    {
        Id = Guid.NewGuid(),
        SenderId = senderId,
        ReceiverId = receiverId,
        HangoutTagId = CoffeeTagId,
        PlaceId = placeId,
        SenderIsThere = false,
        Status = status,
        SentAt = now.AddHours(-1),
        RespondedAt = respondedAt,
        ExpiresAt = expiresAt ?? now.AddHours(47),
        CreatedAt = now.AddHours(-1),
    };

    // -----------------------------------------------------------------------
    // 401
    // -----------------------------------------------------------------------

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.40.1.1");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{Guid.NewGuid()}/accept",
            new { },
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
        const string SUB = "oid|accept-no-user";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.40.1.2");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{Guid.NewGuid()}/accept",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.User.NotRegistered);
    }

    /// <summary>Unknown invite id → 404.</summary>
    [Fact]
    public async Task HandleAsync_UnknownInviteId_Returns404()
    {
        const string RECEIVER_SUB = "oid|accept-unknown-invite-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(RECEIVER_SUB, null, now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(RECEIVER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.40.1.3");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{Guid.NewGuid()}/accept",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Caller is the sender (foreign invite) → 404 not 403.</summary>
    [Fact]
    public async Task HandleAsync_CallerIsSenderNotReceiver_Returns404()
    {
        const string SENDER_SUB = "oid|accept-foreign-sender";
        const string RECEIVER_SUB = "oid|accept-foreign-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid inviteId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var sender = CreateUser(SENDER_SUB, null, now);
            var receiver = CreateUser(RECEIVER_SUB, null, now);
            db.Users.AddRange(sender, receiver);
            var (_, placeId) = SeedCityAndPlace(db, now, "accept-foreign");
            var invite = MakeInvite(sender.Id, receiver.Id, placeId, now);
            inviteId = invite.Id;
            db.Invites.Add(invite);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // sender tries to accept their own outbound invite → 404
        var client = App.CreateAuthenticatedClient(SENDER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.40.1.4");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/accept",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -----------------------------------------------------------------------
    // 409 tests
    // -----------------------------------------------------------------------

    /// <summary>Invite is already Accepted → 409 + Invite.AlreadyResolved.</summary>
    [Fact]
    public async Task HandleAsync_InviteAlreadyAccepted_Returns409WithAlreadyResolved()
    {
        const string SENDER_SUB = "oid|accept-already-accepted-sender";
        const string RECEIVER_SUB = "oid|accept-already-accepted-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid inviteId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var sender = CreateUser(SENDER_SUB, null, now);
            var receiver = CreateUser(RECEIVER_SUB, null, now);
            db.Users.AddRange(sender, receiver);
            var (_, placeId) = SeedCityAndPlace(db, now, "accept-accepted");
            var invite = MakeInvite(sender.Id, receiver.Id, placeId, now,
                status: InviteStatus.Accepted, respondedAt: now.AddMinutes(-5));
            inviteId = invite.Id;
            db.Invites.Add(invite);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(RECEIVER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.40.1.5");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/accept",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Invite.AlreadyResolved);
    }

    /// <summary>Invite is already Declined → 409 + Invite.AlreadyResolved.</summary>
    [Fact]
    public async Task HandleAsync_InviteAlreadyDeclined_Returns409WithAlreadyResolved()
    {
        const string SENDER_SUB = "oid|accept-already-declined-sender";
        const string RECEIVER_SUB = "oid|accept-already-declined-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid inviteId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var sender = CreateUser(SENDER_SUB, null, now);
            var receiver = CreateUser(RECEIVER_SUB, null, now);
            db.Users.AddRange(sender, receiver);
            var (_, placeId) = SeedCityAndPlace(db, now, "accept-declined");
            var invite = MakeInvite(sender.Id, receiver.Id, placeId, now,
                status: InviteStatus.Declined, respondedAt: now.AddMinutes(-5));
            inviteId = invite.Id;
            db.Invites.Add(invite);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(RECEIVER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.40.1.6");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/accept",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Invite.AlreadyResolved);
    }

    /// <summary>Invite is Pending but ExpiresAt is in the past → 409 + Invite.AlreadyResolved.</summary>
    [Fact]
    public async Task HandleAsync_InvitePendingButExpired_Returns409WithAlreadyResolved()
    {
        const string SENDER_SUB = "oid|accept-expired-sender";
        const string RECEIVER_SUB = "oid|accept-expired-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid inviteId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var sender = CreateUser(SENDER_SUB, null, now);
            var receiver = CreateUser(RECEIVER_SUB, null, now);
            db.Users.AddRange(sender, receiver);
            var (_, placeId) = SeedCityAndPlace(db, now, "accept-expired");
            var invite = MakeInvite(sender.Id, receiver.Id, placeId, now,
                status: InviteStatus.Pending, expiresAt: now.AddHours(-1)); // expired
            inviteId = invite.Id;
            db.Invites.Add(invite);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(RECEIVER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.40.1.7");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/accept",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Invite.AlreadyResolved);
    }

    // -----------------------------------------------------------------------
    // Happy path
    // -----------------------------------------------------------------------

    /// <summary>Happy path: 200, DB invite updated to Accepted, Meetup row created with correct FKs.</summary>
    [Fact]
    public async Task HandleAsync_HappyPath_Returns200WithInviteDtoAndMeetupId_AndPersistsCorrectly()
    {
        const string SENDER_SUB = "oid|accept-happy-sender";
        const string RECEIVER_SUB = "oid|accept-happy-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid inviteId;
        Guid senderId;
        Guid receiverId;
        Guid placeId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var sender = CreateUser(SENDER_SUB, null, now);
            var receiver = CreateUser(RECEIVER_SUB, null, now);
            senderId = sender.Id;
            receiverId = receiver.Id;
            db.Users.AddRange(sender, receiver);
            (_, placeId) = SeedCityAndPlace(db, now, "accept-happy");
            var invite = MakeInvite(sender.Id, receiver.Id, placeId, now);
            inviteId = invite.Id;
            db.Invites.Add(invite);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(RECEIVER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.40.1.8");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/accept",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AcceptInviteResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.MeetupId.Should().NotBeEmpty();
        body.Invite.Id.Should().Be(inviteId);
        body.Invite.Status.Should().Be(InviteStatus.Accepted);
        body.Invite.RespondedAt.Should().Be(now);

        // DB assertions
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();

            var invite = await db.Invites.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == inviteId, TestContext.Current.CancellationToken);
            invite.Should().NotBeNull();
            invite!.Status.Should().Be(InviteStatus.Accepted);
            invite.RespondedAt.Should().Be(now);

            var meetup = await db.Meetups.AsNoTracking()
                .FirstOrDefaultAsync(m => m.InviteId == inviteId, TestContext.Current.CancellationToken);
            meetup.Should().NotBeNull();
            meetup!.Id.Should().Be(body.MeetupId);
            meetup.UserAId.Should().Be(senderId);
            meetup.UserBId.Should().Be(receiverId);
            meetup.PlaceId.Should().Be(placeId);
            meetup.MetAt.Should().Be(now);
            meetup.PromptSent.Should().BeFalse();
        }
    }

    /// <summary>Happy path with recording notifier: InviteAcceptedAsync is invoked exactly once with the new meetup id.</summary>
    [Fact]
    public async Task HandleAsync_HappyPath_FiresInviteAcceptedAsyncOnInviteNotifierWithMeetupId()
    {
        const string SENDER_SUB = "oid|accept-fire-sender";
        const string RECEIVER_SUB = "oid|accept-fire-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid inviteId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var sender = CreateUser(SENDER_SUB, null, now);
            var receiver = CreateUser(RECEIVER_SUB, null, now);
            db.Users.AddRange(sender, receiver);
            var (_, placeId) = SeedCityAndPlace(db, now, "accept-fire");
            var invite = MakeInvite(sender.Id, receiver.Id, placeId, now);
            inviteId = invite.Id;
            db.Invites.Add(invite);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var spy = new RecordingInviteNotifier();
        var client = App.CreateAuthenticatedClientWithInviteNotifier(RECEIVER_SUB, spy);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.40.1.13");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/accept",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AcceptInviteResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();

        spy.Accepted.Should().HaveCount(1);
        spy.Accepted[0].Invite.Id.Should().Be(inviteId);
        spy.Accepted[0].MeetupId.Should().Be(body!.MeetupId);
        spy.Sent.Should().BeEmpty();
    }

    /// <summary>Notifier throws on accept → endpoint still returns 200 and invite + meetup are persisted.</summary>
    [Fact]
    public async Task HandleAsync_NotifierThrows_StillReturns200AndPersists()
    {
        const string SENDER_SUB = "oid|accept-throws-sender";
        const string RECEIVER_SUB = "oid|accept-throws-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid inviteId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var sender = CreateUser(SENDER_SUB, null, now);
            var receiver = CreateUser(RECEIVER_SUB, null, now);
            db.Users.AddRange(sender, receiver);
            var (_, placeId) = SeedCityAndPlace(db, now, "accept-throws");
            var invite = MakeInvite(sender.Id, receiver.Id, placeId, now);
            inviteId = invite.Id;
            db.Invites.Add(invite);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var spy = new RecordingInviteNotifier { ThrowOnAccepted = new InvalidOperationException("simulated downstream failure") };
        var client = App.CreateAuthenticatedClientWithInviteNotifier(RECEIVER_SUB, spy);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.40.1.14");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/accept",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verify = App.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var inviteAfter = await verifyDb.Invites.AsNoTracking()
            .FirstAsync(i => i.Id == inviteId, TestContext.Current.CancellationToken);
        inviteAfter.Status.Should().Be(InviteStatus.Accepted);
        var meetupExists = await verifyDb.Meetups.AsNoTracking()
            .AnyAsync(m => m.InviteId == inviteId, TestContext.Current.CancellationToken);
        meetupExists.Should().BeTrue();
    }

    /// <summary>Happy path: TrustScore is unchanged for both sender and receiver. Trust changes only via reviews (UC-302).</summary>
    [Fact]
    public async Task HandleAsync_HappyPath_DoesNotChangeUserTrustScore()
    {
        const string SENDER_SUB = "oid|accept-trust-sender";
        const string RECEIVER_SUB = "oid|accept-trust-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid senderId, receiverId, inviteId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var sender = CreateUser(SENDER_SUB, null, now);
            sender.TrustScore = 42;
            var receiver = CreateUser(RECEIVER_SUB, null, now);
            receiver.TrustScore = 17;
            senderId = sender.Id;
            receiverId = receiver.Id;
            db.Users.AddRange(sender, receiver);
            var (_, placeId) = SeedCityAndPlace(db, now, "accept-trust");
            var invite = MakeInvite(sender.Id, receiver.Id, placeId, now);
            inviteId = invite.Id;
            db.Invites.Add(invite);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(RECEIVER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.40.1.15");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/accept",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verify = App.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var senderAfter = await verifyDb.Users.AsNoTracking().FirstAsync(u => u.Id == senderId, TestContext.Current.CancellationToken);
        var receiverAfter = await verifyDb.Users.AsNoTracking().FirstAsync(u => u.Id == receiverId, TestContext.Current.CancellationToken);
        senderAfter.TrustScore.Should().Be(42);
        receiverAfter.TrustScore.Should().Be(17);
    }

    // Local DTO for deserialization
    private sealed record AcceptInviteResponseDto(InviteItemDto Invite, Guid MeetupId);

    private sealed record InviteItemDto(Guid Id, InviteUserDto Sender, InviteUserDto Receiver,
        Guid HangoutTagId, string HangoutTagSlug, InvitePlaceDto Place, bool SenderIsThere,
        InviteStatus Status, DateTimeOffset SentAt, DateTimeOffset? RespondedAt, DateTimeOffset ExpiresAt);

    private sealed record InviteUserDto(Guid Id, string FirstName, string? PhotoUrl);
    private sealed record InvitePlaceDto(Guid Id, string Name, PlaceCategory Category);
}
