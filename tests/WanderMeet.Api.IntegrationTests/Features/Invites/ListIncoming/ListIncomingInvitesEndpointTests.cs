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

namespace WanderMeet.Api.IntegrationTests.Features.Invites.ListIncoming;

/// <summary>Integration tests for GET /api/v1/invites/incoming.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class ListIncomingInvitesEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
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

    private static async Task<(Guid CityId, Guid PlaceId)> SeedCityAndPlaceAsync(WanderMeetDbContext db, DateTimeOffset now, string suffix)
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
        await Task.CompletedTask;
        return (cityId, placeId);
    }

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.1");

        var response = await client.GetAsync("api/v1/invites/incoming", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>JWT sub has no User row → 404 + User.NotRegistered.</summary>
    [Fact]
    public async Task HandleAsync_JwtSubHasNoUserRow_Returns404WithUserNotRegistered()
    {
        const string SUB = "oid|incoming-no-user";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.2");

        var response = await client.GetAsync("api/v1/invites/incoming", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.User.NotRegistered);
    }

    /// <summary>Caller has no incoming Pending invites → 200 with empty list.</summary>
    [Fact]
    public async Task HandleAsync_NoIncomingPendingInvites_Returns200WithEmptyList()
    {
        const string CALLER_SUB = "oid|incoming-empty-caller";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.3");

        var response = await client.GetAsync("api/v1/invites/incoming", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InviteListDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.Items.Should().BeEmpty();
    }

    /// <summary>Only Pending invites where caller is receiver are returned; Accepted and sender-side are excluded.</summary>
    [Fact]
    public async Task HandleAsync_MixedInvites_OnlyPendingReceivedReturned()
    {
        const string CALLER_SUB = "oid|incoming-happy-caller";
        const string SENDER_SUB = "oid|incoming-happy-sender";
        const string OTHER_SUB = "oid|incoming-happy-other";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid callerId;
        Guid pendingInviteId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var caller = CreateUser(CALLER_SUB, now);
            callerId = caller.Id;
            var sender = CreateUser(SENDER_SUB, now);
            var other = CreateUser(OTHER_SUB, now);
            db.Users.AddRange(caller, sender, other);
            var (_, placeId) = await SeedCityAndPlaceAsync(db, now, "incoming-happy");

            // Pending invite: caller is receiver — SHOULD be returned
            pendingInviteId = Guid.NewGuid();
            db.Invites.Add(new Invite
            {
                Id = pendingInviteId,
                SenderId = sender.Id,
                ReceiverId = callerId,
                HangoutTagId = CoffeeTagId,
                PlaceId = placeId,
                Status = InviteStatus.Pending,
                SentAt = now.AddHours(-1),
                ExpiresAt = now.AddHours(47),
                CreatedAt = now.AddHours(-1),
            });
            // Accepted invite: caller is receiver — should NOT be returned
            db.Invites.Add(new Invite
            {
                Id = Guid.NewGuid(),
                SenderId = sender.Id,
                ReceiverId = callerId,
                HangoutTagId = CoffeeTagId,
                PlaceId = placeId,
                Status = InviteStatus.Accepted,
                SentAt = now.AddHours(-3),
                ExpiresAt = now.AddHours(45),
                RespondedAt = now.AddHours(-2),
                CreatedAt = now.AddHours(-3),
            });
            // Pending invite: caller is SENDER — should NOT be returned
            db.Invites.Add(new Invite
            {
                Id = Guid.NewGuid(),
                SenderId = callerId,
                ReceiverId = other.Id,
                HangoutTagId = CoffeeTagId,
                PlaceId = placeId,
                Status = InviteStatus.Pending,
                SentAt = now.AddHours(-2),
                ExpiresAt = now.AddHours(46),
                CreatedAt = now.AddHours(-2),
            });

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.4");

        var response = await client.GetAsync("api/v1/invites/incoming", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InviteListDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(1);
        body.Items[0].Id.Should().Be(pendingInviteId);
    }

    /// <summary>Invite where sender is soft-deleted is excluded.</summary>
    [Fact]
    public async Task HandleAsync_SoftDeletedSender_IsExcluded()
    {
        const string CALLER_SUB = "oid|incoming-softdel-caller";
        const string DELETED_SENDER_SUB = "oid|incoming-softdel-sender";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var caller = CreateUser(CALLER_SUB, now);
            var deletedSender = CreateUser(DELETED_SENDER_SUB, now, deleted: true);
            db.Users.AddRange(caller, deletedSender);
            var (_, placeId) = await SeedCityAndPlaceAsync(db, now, "incoming-softdel");

            db.Invites.Add(new Invite
            {
                Id = Guid.NewGuid(),
                SenderId = deletedSender.Id,
                ReceiverId = caller.Id,
                HangoutTagId = CoffeeTagId,
                PlaceId = placeId,
                Status = InviteStatus.Pending,
                SentAt = now.AddHours(-1),
                ExpiresAt = now.AddHours(47),
                CreatedAt = now.AddHours(-1),
            });

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.5");

        var response = await client.GetAsync("api/v1/invites/incoming", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InviteListDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
    }

    /// <summary>Multiple pending invites are returned ordered by SentAt descending.</summary>
    [Fact]
    public async Task HandleAsync_MultiplePendingInvites_OrderedBySentAtDescending()
    {
        const string CALLER_SUB = "oid|incoming-sort-caller";
        const string SENDER1_SUB = "oid|incoming-sort-sender1";
        const string SENDER2_SUB = "oid|incoming-sort-sender2";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid firstInviteId;
        Guid secondInviteId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var caller = CreateUser(CALLER_SUB, now);
            var sender1 = CreateUser(SENDER1_SUB, now);
            var sender2 = CreateUser(SENDER2_SUB, now);
            db.Users.AddRange(caller, sender1, sender2);
            var (_, placeId) = await SeedCityAndPlaceAsync(db, now, "incoming-sort");

            // Older invite
            secondInviteId = Guid.NewGuid();
            db.Invites.Add(new Invite
            {
                Id = secondInviteId,
                SenderId = sender1.Id,
                ReceiverId = caller.Id,
                HangoutTagId = CoffeeTagId,
                PlaceId = placeId,
                Status = InviteStatus.Pending,
                SentAt = now.AddHours(-3),
                ExpiresAt = now.AddHours(45),
                CreatedAt = now.AddHours(-3),
            });
            // Newer invite
            firstInviteId = Guid.NewGuid();
            db.Invites.Add(new Invite
            {
                Id = firstInviteId,
                SenderId = sender2.Id,
                ReceiverId = caller.Id,
                HangoutTagId = CoffeeTagId,
                PlaceId = placeId,
                Status = InviteStatus.Pending,
                SentAt = now.AddHours(-1),
                ExpiresAt = now.AddHours(47),
                CreatedAt = now.AddHours(-1),
            });

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.1.6");

        var response = await client.GetAsync("api/v1/invites/incoming", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InviteListDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(2);
        body.Items[0].Id.Should().Be(firstInviteId);  // most recent first
        body.Items[1].Id.Should().Be(secondInviteId);
    }

    // Local DTO for deserialization
    private sealed record InviteListDto(IReadOnlyList<InviteItemDto> Items);
    private sealed record InviteItemDto(Guid Id, InviteUserDto Sender, InviteUserDto Receiver,
        Guid HangoutTagId, string HangoutTagSlug, InvitePlaceDto Place, bool SenderIsThere,
        InviteStatus Status, DateTimeOffset SentAt, DateTimeOffset? RespondedAt, DateTimeOffset ExpiresAt);
    private sealed record InviteUserDto(Guid Id, string FirstName, string? PhotoUrl);
    private sealed record InvitePlaceDto(Guid Id, string Name, PlaceCategory Category);
}
