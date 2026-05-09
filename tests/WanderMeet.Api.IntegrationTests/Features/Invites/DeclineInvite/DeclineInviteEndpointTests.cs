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

namespace WanderMeet.Api.IntegrationTests.Features.Invites.DeclineInvite;

/// <summary>Integration tests for PATCH /api/v1/invites/{id}/decline.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class DeclineInviteEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private static readonly Guid CoffeeTagId = new("00000000-0000-0000-0000-0000000000D1");

    private static Point CityCenter() => new(14.42, 50.08) { SRID = 4326 };

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static User CreateUser(string sub, Guid? cityId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        AzureAdB2CId = sub,
        FirstName = "User-" + sub,
        CreatedAt = now,
        LastActiveAt = now,
        CityId = cityId,
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
        InviteStatus status = InviteStatus.Pending, DateTimeOffset? respondedAt = null) => new()
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
        ExpiresAt = now.AddHours(47),
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
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.1.1");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{Guid.NewGuid()}/decline",
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
        const string SUB = "oid|decline-no-user";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.1.2");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{Guid.NewGuid()}/decline",
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
        const string RECEIVER_SUB = "oid|decline-unknown-invite-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(RECEIVER_SUB, null, now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(RECEIVER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.1.3");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{Guid.NewGuid()}/decline",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Caller is the sender (foreign invite) → 404 not 403.</summary>
    [Fact]
    public async Task HandleAsync_CallerIsSenderNotReceiver_Returns404()
    {
        const string SENDER_SUB = "oid|decline-foreign-sender";
        const string RECEIVER_SUB = "oid|decline-foreign-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid inviteId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var sender = CreateUser(SENDER_SUB, null, now);
            var receiver = CreateUser(RECEIVER_SUB, null, now);
            db.Users.AddRange(sender, receiver);
            var (_, placeId) = SeedCityAndPlace(db, now, "decline-foreign");
            var invite = MakeInvite(sender.Id, receiver.Id, placeId, now);
            inviteId = invite.Id;
            db.Invites.Add(invite);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // sender tries to decline their own outbound invite → 404
        var client = App.CreateAuthenticatedClient(SENDER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.1.4");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/decline",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -----------------------------------------------------------------------
    // 409 tests
    // -----------------------------------------------------------------------

    /// <summary>Invite is already Declined → 409 + Invite.AlreadyResolved.</summary>
    [Fact]
    public async Task HandleAsync_InviteAlreadyDeclined_Returns409WithAlreadyResolved()
    {
        const string SENDER_SUB = "oid|decline-already-declined-sender";
        const string RECEIVER_SUB = "oid|decline-already-declined-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid inviteId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var sender = CreateUser(SENDER_SUB, null, now);
            var receiver = CreateUser(RECEIVER_SUB, null, now);
            db.Users.AddRange(sender, receiver);
            var (_, placeId) = SeedCityAndPlace(db, now, "decline-declined");
            var invite = MakeInvite(sender.Id, receiver.Id, placeId, now,
                status: InviteStatus.Declined, respondedAt: now.AddMinutes(-5));
            inviteId = invite.Id;
            db.Invites.Add(invite);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(RECEIVER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.1.5");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/decline",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Invite.AlreadyResolved);
    }

    /// <summary>Invite is already Accepted → 409 + Invite.AlreadyResolved.</summary>
    [Fact]
    public async Task HandleAsync_InviteAlreadyAccepted_Returns409WithAlreadyResolved()
    {
        const string SENDER_SUB = "oid|decline-already-accepted-sender";
        const string RECEIVER_SUB = "oid|decline-already-accepted-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid inviteId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var sender = CreateUser(SENDER_SUB, null, now);
            var receiver = CreateUser(RECEIVER_SUB, null, now);
            db.Users.AddRange(sender, receiver);
            var (_, placeId) = SeedCityAndPlace(db, now, "decline-accepted");
            var invite = MakeInvite(sender.Id, receiver.Id, placeId, now,
                status: InviteStatus.Accepted, respondedAt: now.AddMinutes(-5));
            inviteId = invite.Id;
            db.Invites.Add(invite);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(RECEIVER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.1.6");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/decline",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Invite.AlreadyResolved);
    }

    // -----------------------------------------------------------------------
    // Happy path
    // -----------------------------------------------------------------------

    /// <summary>Happy path: 200 + InviteDto, DB invite updated to Declined, no Meetup row created.</summary>
    [Fact]
    public async Task HandleAsync_HappyPath_Returns200WithInviteDto_NoMeetupCreated()
    {
        const string SENDER_SUB = "oid|decline-happy-sender";
        const string RECEIVER_SUB = "oid|decline-happy-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid inviteId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var sender = CreateUser(SENDER_SUB, null, now);
            var receiver = CreateUser(RECEIVER_SUB, null, now);
            db.Users.AddRange(sender, receiver);
            var (_, placeId) = SeedCityAndPlace(db, now, "decline-happy");
            var invite = MakeInvite(sender.Id, receiver.Id, placeId, now);
            inviteId = invite.Id;
            db.Invites.Add(invite);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(RECEIVER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.1.7");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/decline",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DeclineInviteResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.Id.Should().Be(inviteId);
        body.Status.Should().Be(InviteStatus.Declined);
        body.RespondedAt.Should().Be(now);

        // DB assertions
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();

            var invite = await db.Invites.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == inviteId, TestContext.Current.CancellationToken);
            invite.Should().NotBeNull();
            invite!.Status.Should().Be(InviteStatus.Declined);
            invite.RespondedAt.Should().Be(now);

            // No Meetup row must be created
            var meetupExists = await db.Meetups.AsNoTracking()
                .AnyAsync(m => m.InviteId == inviteId, TestContext.Current.CancellationToken);
            meetupExists.Should().BeFalse();
        }
    }

    /// <summary>Happy path fires InviteDeclinedAsync on the notifier with the persisted invite Id; Sent and Accepted remain empty.</summary>
    [Fact]
    public async Task HandleAsync_HappyPath_FiresInviteDeclinedAsyncOnInviteNotifier()
    {
        const string SENDER_SUB = "oid|decline-fires-sender";
        const string RECEIVER_SUB = "oid|decline-fires-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid inviteId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var sender = CreateUser(SENDER_SUB, null, now);
            var receiver = CreateUser(RECEIVER_SUB, null, now);
            db.Users.AddRange(sender, receiver);
            var (_, placeId) = SeedCityAndPlace(db, now, "decline-fires");
            var invite = MakeInvite(sender.Id, receiver.Id, placeId, now);
            inviteId = invite.Id;
            db.Invites.Add(invite);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var spy = new RecordingInviteNotifier();
        var client = App.CreateAuthenticatedClientWithInviteNotifier(RECEIVER_SUB, spy);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.1.11");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/decline",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        spy.Declined.Should().HaveCount(1);
        spy.Declined[0].Id.Should().Be(inviteId);
        spy.Sent.Should().BeEmpty();
        spy.Accepted.Should().BeEmpty();
    }

    /// <summary>Notifier throws on decline → endpoint still returns 200 and invite is persisted as Declined.</summary>
    [Fact]
    public async Task HandleAsync_NotifierThrows_StillReturns200AndPersists()
    {
        const string SENDER_SUB = "oid|decline-throws-sender";
        const string RECEIVER_SUB = "oid|decline-throws-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid inviteId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var sender = CreateUser(SENDER_SUB, null, now);
            var receiver = CreateUser(RECEIVER_SUB, null, now);
            db.Users.AddRange(sender, receiver);
            var (_, placeId) = SeedCityAndPlace(db, now, "decline-throws");
            var invite = MakeInvite(sender.Id, receiver.Id, placeId, now);
            inviteId = invite.Id;
            db.Invites.Add(invite);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var spy = new RecordingInviteNotifier { ThrowOnDeclined = new InvalidOperationException("simulated downstream failure") };
        var client = App.CreateAuthenticatedClientWithInviteNotifier(RECEIVER_SUB, spy);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.1.12");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/decline",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verify = App.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var inviteAfter = await verifyDb.Invites.AsNoTracking()
            .FirstAsync(i => i.Id == inviteId, TestContext.Current.CancellationToken);
        inviteAfter.Status.Should().Be(InviteStatus.Declined);
        inviteAfter.RespondedAt.Should().Be(now);
    }

    /// <summary>Subset of InviteDto used to deserialise the decline-happy-path response body.</summary>
    private sealed record DeclineInviteResponseDto(Guid Id, InviteStatus Status, DateTimeOffset? RespondedAt);
}
