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

namespace WanderMeet.Api.IntegrationTests.Features.Invites.ListPast;

/// <summary>Integration tests for GET /api/v1/invites/past.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class ListPastInvitesEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
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
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.3.1");

        var response = await client.GetAsync("api/v1/invites/past", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>JWT sub has no User row → 404 + User.NotRegistered.</summary>
    [Fact]
    public async Task HandleAsync_JwtSubHasNoUserRow_Returns404WithUserNotRegistered()
    {
        const string SUB = "oid|past-no-user";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.3.2");

        var response = await client.GetAsync("api/v1/invites/past", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.User.NotRegistered);
    }

    /// <summary>Caller has no past invites → 200 with empty list.</summary>
    [Fact]
    public async Task HandleAsync_NoPastInvites_Returns200WithEmptyList()
    {
        const string CALLER_SUB = "oid|past-empty-caller";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.3.3");

        var response = await client.GetAsync("api/v1/invites/past", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InviteListDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
    }

    /// <summary>Pending invites are excluded; non-Pending invites where caller is sender or receiver are returned.</summary>
    [Fact]
    public async Task HandleAsync_MixedStatuses_PendingExcludedNonPendingReturned()
    {
        const string CALLER_SUB = "oid|past-mixed-caller";
        const string OTHER_SUB = "oid|past-mixed-other";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid acceptedId;
        Guid declinedId;
        Guid expiredId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var caller = CreateUser(CALLER_SUB, now);
            var other = CreateUser(OTHER_SUB, now);
            db.Users.AddRange(caller, other);
            var (_, placeId) = SeedCityAndPlace(db, now, "past-mixed");

            // Caller as sender: Accepted
            acceptedId = Guid.NewGuid();
            db.Invites.Add(new Invite
            {
                Id = acceptedId, SenderId = caller.Id, ReceiverId = other.Id,
                HangoutTagId = CoffeeTagId, PlaceId = placeId, Status = InviteStatus.Accepted,
                SentAt = now.AddHours(-5), ExpiresAt = now.AddHours(43), RespondedAt = now.AddHours(-4),
                CreatedAt = now.AddHours(-5),
            });
            // Caller as receiver: Declined
            declinedId = Guid.NewGuid();
            db.Invites.Add(new Invite
            {
                Id = declinedId, SenderId = other.Id, ReceiverId = caller.Id,
                HangoutTagId = CoffeeTagId, PlaceId = placeId, Status = InviteStatus.Declined,
                SentAt = now.AddHours(-3), ExpiresAt = now.AddHours(45), RespondedAt = now.AddHours(-2),
                CreatedAt = now.AddHours(-3),
            });
            // Caller as sender: Expired (RespondedAt = null)
            expiredId = Guid.NewGuid();
            db.Invites.Add(new Invite
            {
                Id = expiredId, SenderId = caller.Id, ReceiverId = other.Id,
                HangoutTagId = CoffeeTagId, PlaceId = placeId, Status = InviteStatus.Expired,
                SentAt = now.AddHours(-50), ExpiresAt = now.AddHours(-2), RespondedAt = null,
                CreatedAt = now.AddHours(-50),
            });
            // Caller as sender: Pending — should NOT appear in /past
            db.Invites.Add(new Invite
            {
                Id = Guid.NewGuid(), SenderId = caller.Id, ReceiverId = other.Id,
                HangoutTagId = CoffeeTagId, PlaceId = placeId, Status = InviteStatus.Pending,
                SentAt = now.AddHours(-1), ExpiresAt = now.AddHours(47), CreatedAt = now.AddHours(-1),
            });

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.3.4");

        var response = await client.GetAsync("api/v1/invites/past", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InviteListDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(3);
        body.Items.Select(i => i.Id).Should().Contain([acceptedId, declinedId, expiredId]);
    }

    /// <summary>
    /// NULLS-LAST ordering: invite with RespondedAt = null appears after invites with populated RespondedAt.
    /// Two responded invites are sorted by RespondedAt DESC.
    /// </summary>
    [Fact]
    public async Task HandleAsync_RespondedAtNullsLast_OrderedByRespondedAtDescThenSentAtDesc()
    {
        const string CALLER_SUB = "oid|past-nullslast-caller";
        const string OTHER_SUB = "oid|past-nullslast-other";
        var now = App.FakeTimeProvider.GetUtcNow();

        Guid firstId;   // RespondedAt = now - 1h (most recent response)
        Guid secondId;  // RespondedAt = now - 3h
        Guid thirdId;   // RespondedAt = null (Expired, should be last)

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var caller = CreateUser(CALLER_SUB, now);
            var other = CreateUser(OTHER_SUB, now);
            db.Users.AddRange(caller, other);
            var (_, placeId) = SeedCityAndPlace(db, now, "past-nullslast");

            // First: responded most recently
            firstId = Guid.NewGuid();
            db.Invites.Add(new Invite
            {
                Id = firstId, SenderId = caller.Id, ReceiverId = other.Id,
                HangoutTagId = CoffeeTagId, PlaceId = placeId, Status = InviteStatus.Accepted,
                SentAt = now.AddHours(-4), ExpiresAt = now.AddHours(44), RespondedAt = now.AddHours(-1),
                CreatedAt = now.AddHours(-4),
            });
            // Second: responded less recently
            secondId = Guid.NewGuid();
            db.Invites.Add(new Invite
            {
                Id = secondId, SenderId = caller.Id, ReceiverId = other.Id,
                HangoutTagId = CoffeeTagId, PlaceId = placeId, Status = InviteStatus.Declined,
                SentAt = now.AddHours(-6), ExpiresAt = now.AddHours(42), RespondedAt = now.AddHours(-3),
                CreatedAt = now.AddHours(-6),
            });
            // Third: no response (expired) — should be LAST
            thirdId = Guid.NewGuid();
            db.Invites.Add(new Invite
            {
                Id = thirdId, SenderId = caller.Id, ReceiverId = other.Id,
                HangoutTagId = CoffeeTagId, PlaceId = placeId, Status = InviteStatus.Expired,
                SentAt = now.AddHours(-50), ExpiresAt = now.AddHours(-2), RespondedAt = null,
                CreatedAt = now.AddHours(-50),
            });

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.3.5");

        var response = await client.GetAsync("api/v1/invites/past", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InviteListDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(3);
        body.Items[0].Id.Should().Be(firstId);   // RespondedAt most recent
        body.Items[1].Id.Should().Be(secondId);  // RespondedAt less recent
        body.Items[2].Id.Should().Be(thirdId);   // null RespondedAt = LAST
    }

    /// <summary>Other users' non-Pending invites (not involving caller) are not leaked.</summary>
    [Fact]
    public async Task HandleAsync_OtherUsersInvites_NotLeaked()
    {
        const string CALLER_SUB = "oid|past-isolation-caller";
        const string USER_A_SUB = "oid|past-isolation-a";
        const string USER_B_SUB = "oid|past-isolation-b";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await EnsureCoffeeTagAsync(db, now);
            var caller = CreateUser(CALLER_SUB, now);
            var userA = CreateUser(USER_A_SUB, now);
            var userB = CreateUser(USER_B_SUB, now);
            db.Users.AddRange(caller, userA, userB);
            var (_, placeId) = SeedCityAndPlace(db, now, "past-isolation");

            // Invite between A and B — caller is not involved
            db.Invites.Add(new Invite
            {
                Id = Guid.NewGuid(), SenderId = userA.Id, ReceiverId = userB.Id,
                HangoutTagId = CoffeeTagId, PlaceId = placeId, Status = InviteStatus.Accepted,
                SentAt = now.AddHours(-5), ExpiresAt = now.AddHours(43), RespondedAt = now.AddHours(-4),
                CreatedAt = now.AddHours(-5),
            });

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.3.6");

        var response = await client.GetAsync("api/v1/invites/past", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InviteListDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
    }

    // Local DTO for deserialization
    private sealed record InviteListDto(IReadOnlyList<InviteItemDto> Items);
    private sealed record InviteItemDto(Guid Id, InviteUserDto Sender, InviteUserDto Receiver,
        Guid HangoutTagId, string HangoutTagSlug, InvitePlaceDto Place, bool SenderIsThere,
        InviteStatus Status, DateTimeOffset SentAt, DateTimeOffset? RespondedAt, DateTimeOffset ExpiresAt);
    private sealed record InviteUserDto(Guid Id, string FirstName, string? PhotoUrl);
    private sealed record InvitePlaceDto(Guid Id, string Name, PlaceCategory Category);
}
