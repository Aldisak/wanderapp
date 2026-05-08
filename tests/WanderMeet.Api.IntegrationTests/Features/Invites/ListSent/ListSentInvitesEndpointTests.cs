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

namespace WanderMeet.Api.IntegrationTests.Features.Invites.ListSent;

/// <summary>Integration tests for GET /api/v1/invites/sent.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class ListSentInvitesEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private static readonly Guid CoffeeTagId = new("00000000-0000-0000-0000-000000000001");

    private static User CreateUser(string sub, DateTimeOffset now, bool deleted = false) => new()
    {
        Id = Guid.NewGuid(),
        AzureAdB2CId = sub,
        FirstName = "User-" + sub,
        CreatedAt = now,
        LastActiveAt = now,
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
        var point = new Point(14.42, 50.08) { SRID = 4326 };
        db.Cities.Add(new City
        {
            Id = cityId,
            Name = "City-" + suffix,
            Country = "CZ",
            Location = point,
            CreatedAt = now,
        });
        var placeId = Guid.NewGuid();
        db.Places.Add(new Place
        {
            Id = placeId,
            GooglePlaceId = "g-" + suffix,
            Name = "Place-" + suffix,
            CityId = cityId,
            Location = point,
            Category = PlaceCategory.Cafe,
            CreatedAt = now,
        });
        return (cityId, placeId);
    }

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.2.1");

        var response = await client.GetAsync("api/v1/invites/sent", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>JWT sub has no User row → 404 + User.NotRegistered.</summary>
    [Fact]
    public async Task HandleAsync_JwtSubHasNoUserRow_Returns404WithUserNotRegistered()
    {
        const string SUB = "oid|sent-no-user";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.2.2");

        var response = await client.GetAsync("api/v1/invites/sent", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.User.NotRegistered);
    }

    /// <summary>Caller has no sent invites → 200 with empty list.</summary>
    [Fact]
    public async Task HandleAsync_NoSentInvites_Returns200WithEmptyList()
    {
        const string CALLER_SUB = "oid|sent-empty-caller";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.2.3");

        var response = await client.GetAsync("api/v1/invites/sent", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InviteListDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
    }

    /// <summary>All statuses returned for sent invites; receiver-side invite excluded.</summary>
    [Fact]
    public async Task HandleAsync_AllStatusesSentByCallerReturned_ReceiverSideExcluded()
    {
        const string CALLER_SUB = "oid|sent-allstatus-caller";
        const string OTHER_SUB = "oid|sent-allstatus-other";
        const string SENDER_SUB = "oid|sent-allstatus-sender";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var caller = CreateUser(CALLER_SUB, now);
            var other = CreateUser(OTHER_SUB, now);
            var sender = CreateUser(SENDER_SUB, now);
            db.Users.AddRange(caller, other, sender);
            var (_, placeId) = SeedCityAndPlace(db, now, "sent-allstatus");

            // Sent by caller with 3 different statuses - all should be returned
            db.Invites.Add(new Invite
            {
                Id = Guid.NewGuid(), SenderId = caller.Id, ReceiverId = other.Id,
                HangoutTagId = CoffeeTagId, PlaceId = placeId, Status = InviteStatus.Pending,
                SentAt = now.AddHours(-1), ExpiresAt = now.AddHours(47), CreatedAt = now.AddHours(-1),
            });
            db.Invites.Add(new Invite
            {
                Id = Guid.NewGuid(), SenderId = caller.Id, ReceiverId = other.Id,
                HangoutTagId = CoffeeTagId, PlaceId = placeId, Status = InviteStatus.Accepted,
                SentAt = now.AddHours(-3), ExpiresAt = now.AddHours(45), RespondedAt = now.AddHours(-2),
                CreatedAt = now.AddHours(-3),
            });
            db.Invites.Add(new Invite
            {
                Id = Guid.NewGuid(), SenderId = caller.Id, ReceiverId = other.Id,
                HangoutTagId = CoffeeTagId, PlaceId = placeId, Status = InviteStatus.Declined,
                SentAt = now.AddHours(-5), ExpiresAt = now.AddHours(43), RespondedAt = now.AddHours(-4),
                CreatedAt = now.AddHours(-5),
            });
            // Invite RECEIVED by caller - should NOT be returned
            db.Invites.Add(new Invite
            {
                Id = Guid.NewGuid(), SenderId = sender.Id, ReceiverId = caller.Id,
                HangoutTagId = CoffeeTagId, PlaceId = placeId, Status = InviteStatus.Pending,
                SentAt = now.AddHours(-2), ExpiresAt = now.AddHours(46), CreatedAt = now.AddHours(-2),
            });

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.2.4");

        var response = await client.GetAsync("api/v1/invites/sent", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InviteListDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(3);
    }

    /// <summary>Sent invites are ordered by SentAt descending.</summary>
    [Fact]
    public async Task HandleAsync_MultipleSentInvites_OrderedBySentAtDescending()
    {
        const string CALLER_SUB = "oid|sent-sort-caller";
        const string OTHER_SUB = "oid|sent-sort-other";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid oldestId;
        Guid middleId;
        Guid newestId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var caller = CreateUser(CALLER_SUB, now);
            var other = CreateUser(OTHER_SUB, now);
            db.Users.AddRange(caller, other);
            var (_, placeId) = SeedCityAndPlace(db, now, "sent-sort");

            oldestId = Guid.NewGuid();
            db.Invites.Add(new Invite
            {
                Id = oldestId, SenderId = caller.Id, ReceiverId = other.Id,
                HangoutTagId = CoffeeTagId, PlaceId = placeId, Status = InviteStatus.Declined,
                SentAt = now.AddHours(-5), ExpiresAt = now.AddHours(43), RespondedAt = now.AddHours(-4),
                CreatedAt = now.AddHours(-5),
            });
            middleId = Guid.NewGuid();
            db.Invites.Add(new Invite
            {
                Id = middleId, SenderId = caller.Id, ReceiverId = other.Id,
                HangoutTagId = CoffeeTagId, PlaceId = placeId, Status = InviteStatus.Accepted,
                SentAt = now.AddHours(-3), ExpiresAt = now.AddHours(45), RespondedAt = now.AddHours(-2),
                CreatedAt = now.AddHours(-3),
            });
            newestId = Guid.NewGuid();
            db.Invites.Add(new Invite
            {
                Id = newestId, SenderId = caller.Id, ReceiverId = other.Id,
                HangoutTagId = CoffeeTagId, PlaceId = placeId, Status = InviteStatus.Pending,
                SentAt = now.AddHours(-1), ExpiresAt = now.AddHours(47), CreatedAt = now.AddHours(-1),
            });

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.2.5");

        var response = await client.GetAsync("api/v1/invites/sent", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InviteListDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(3);
        body.Items[0].Id.Should().Be(newestId);
        body.Items[1].Id.Should().Be(middleId);
        body.Items[2].Id.Should().Be(oldestId);
    }

    /// <summary>Invite where receiver is soft-deleted is excluded from sent list.</summary>
    [Fact]
    public async Task HandleAsync_SoftDeletedReceiver_IsExcluded()
    {
        const string CALLER_SUB = "oid|sent-softdel-caller";
        const string DELETED_RECEIVER_SUB = "oid|sent-softdel-receiver";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var caller = CreateUser(CALLER_SUB, now);
            var deletedReceiver = CreateUser(DELETED_RECEIVER_SUB, now, deleted: true);
            db.Users.AddRange(caller, deletedReceiver);
            var (_, placeId) = SeedCityAndPlace(db, now, "sent-softdel");

            db.Invites.Add(new Invite
            {
                Id = Guid.NewGuid(), SenderId = caller.Id, ReceiverId = deletedReceiver.Id,
                HangoutTagId = CoffeeTagId, PlaceId = placeId, Status = InviteStatus.Pending,
                SentAt = now.AddHours(-1), ExpiresAt = now.AddHours(47), CreatedAt = now.AddHours(-1),
            });

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.2.6");

        var response = await client.GetAsync("api/v1/invites/sent", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InviteListDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
    }

    /// <summary>Take(50) cap: 60 sent invites → response has exactly 50.</summary>
    [Fact]
    public async Task HandleAsync_SixtyMatchingInvites_ReturnsExactlyFifty()
    {
        const string CALLER_SUB = "oid|sent-cap-caller";
        const string RECEIVER_SUB = "oid|sent-cap-receiver";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var caller = CreateUser(CALLER_SUB, now);
            var receiver = CreateUser(RECEIVER_SUB, now);
            db.Users.AddRange(caller, receiver);
            var (_, placeId) = SeedCityAndPlace(db, now, "sent-cap");
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Seed 60 invites
            for (var i = 0; i < 60; i++)
            {
                db.Invites.Add(new Invite
                {
                    Id = Guid.NewGuid(), SenderId = caller.Id, ReceiverId = receiver.Id,
                    HangoutTagId = CoffeeTagId, PlaceId = placeId, Status = InviteStatus.Declined,
                    SentAt = now.AddHours(-i - 1), ExpiresAt = now.AddHours(47 - i),
                    RespondedAt = now.AddHours(-i), CreatedAt = now.AddHours(-i - 1),
                });
            }

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.2.7");

        var response = await client.GetAsync("api/v1/invites/sent", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InviteListDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(50);
    }

    // Local DTO for deserialization
    private sealed record InviteListDto(IReadOnlyList<InviteItemDto> Items);
    private sealed record InviteItemDto(Guid Id, InviteUserDto Sender, InviteUserDto Receiver,
        Guid HangoutTagId, string HangoutTagSlug, InvitePlaceDto Place, bool SenderIsThere,
        InviteStatus Status, DateTimeOffset SentAt, DateTimeOffset? RespondedAt, DateTimeOffset ExpiresAt);
    private sealed record InviteUserDto(Guid Id, string FirstName, string? PhotoUrl);
    private sealed record InvitePlaceDto(Guid Id, string Name, PlaceCategory Category);
}
