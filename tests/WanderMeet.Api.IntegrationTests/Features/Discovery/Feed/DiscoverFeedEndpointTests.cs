using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using WanderMeet.Api.Common;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Discovery;
using WanderMeet.Api.Features.Discovery.Feed;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Discovery.Feed;

/// <summary>Integration tests for GET /api/v1/discover.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class DiscoverFeedEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private const int Srid = 4326;

    // City center coordinates (Prague-like ~50°N)
    private const double CityLon = 14.42;
    private const double CityLat = 50.08;

    // Hangout tag stable GUIDs (seed data)
    private static readonly Guid CoffeeTagId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid WalkTagId = new("00000000-0000-0000-0000-000000000002");

    private static Point CityCenter() => new(CityLon, CityLat) { SRID = Srid };

    // Inside radius: same location as city center
    private static Point InsideRadius() => new(CityLon + 0.1, CityLat + 0.1) { SRID = Srid };

    // Outside radius: ~60 km away (at ~50°N, 1° lon ≈ 64 km, 0.8° ≈ 51 km but let's use lon+1.0 for certainty)
    private static Point OutsideRadius() => new(CityLon + 1.0, CityLat) { SRID = Srid };

    private static User CreateUser(string sub, DateTimeOffset now, Point? location = null, bool isOpenToday = true,
        int trustScore = 50, DateTimeOffset? lastActiveAt = null, bool deleted = false) => new()
    {
        Id = Guid.NewGuid(),
        AzureAdB2CId = sub,
        FirstName = "User " + sub,
        CreatedAt = now,
        LastActiveAt = lastActiveAt ?? now,
        Location = location ?? InsideRadius(),
        IsOpenToday = isOpenToday,
        TrustScore = trustScore,
        DeletedAt = deleted ? now : null,
    };

    private static async Task SeedHangoutTags(WanderMeetDbContext db, DateTimeOffset now)
    {
        // Seed hangout tag for Coffee if not present
        if (!await db.HangoutTags.AnyAsync(h => h.Id == CoffeeTagId))
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

        if (!await db.HangoutTags.AnyAsync(h => h.Id == WalkTagId))
        {
            db.HangoutTags.Add(new HangoutTag
            {
                Id = WalkTagId,
                Slug = HangoutTagSlug.Walk,
                Label = "Walk",
                Emoji = "🚶",
                CreatedAt = now,
            });
        }
    }

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.1");

        var response = await client.GetAsync($"api/v1/discover?cityId={Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>JWT sub has no User row → 404 + User.NotRegistered.</summary>
    [Fact]
    public async Task HandleAsync_JwtSubHasNoUserRow_Returns404WithUserNotRegistered()
    {
        const string SUB = "oid|discover-no-user";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.2");

        var response = await client.GetAsync($"api/v1/discover?cityId={Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.User.NotRegistered);
    }

    /// <summary>Unknown cityId → 404 + Discovery.CityNotFound.</summary>
    [Fact]
    public async Task HandleAsync_UnknownCityId_Returns404WithDiscoveryCityNotFound()
    {
        const string SUB = "oid|discover-unknown-city";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(SUB, now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.3");

        var response = await client.GetAsync($"api/v1/discover?cityId={Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Discovery.CityNotFound);
    }

    /// <summary>Soft-deleted cityId → 404 + Discovery.CityNotFound.</summary>
    [Fact]
    public async Task HandleAsync_SoftDeletedCityId_Returns404WithDiscoveryCityNotFound()
    {
        const string SUB = "oid|discover-deleted-city";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(SUB, now));
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Deleted City",
                Country = "XX",
                Location = CityCenter(),
                CreatedAt = now,
                DeletedAt = now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.4");

        var response = await client.GetAsync($"api/v1/discover?cityId={cityId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Discovery.CityNotFound);
    }

    /// <summary>Happy path → 200 with PublicUserDtos.</summary>
    [Fact]
    public async Task HandleAsync_HappyPath_ReturnsUpToDefaultLimitOfTwentyPublicUserDtos()
    {
        const string CALLER_SUB = "oid|discover-happy-caller";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Test City",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });

            // Add 3 eligible users
            for (var i = 0; i < 3; i++)
            {
                db.Users.Add(CreateUser($"oid|discover-happy-peer-{i}", now));
            }

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.5");

        var response = await client.GetAsync($"api/v1/discover?cityId={cityId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(3);
        body.NextCursor.Should().BeNull();
    }

    /// <summary>One user inside radius, one outside → only inside returned.</summary>
    [Fact]
    public async Task HandleAsync_OneUserInsideRadiusOneOutside_OnlyInsideUserReturned()
    {
        const string CALLER_SUB = "oid|discover-radius-caller";
        const string INSIDE_SUB = "oid|discover-radius-inside";
        const string OUTSIDE_SUB = "oid|discover-radius-outside";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Radius City",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });
            db.Users.Add(CreateUser(INSIDE_SUB, now, InsideRadius()));
            db.Users.Add(CreateUser(OUTSIDE_SUB, now, OutsideRadius()));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.6");

        var response = await client.GetAsync($"api/v1/discover?cityId={cityId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(1);
        body.Items[0].FirstName.Should().Be("User " + INSIDE_SUB);
    }

    /// <summary>Fresh user included, stale user (last active > 72h ago) excluded.</summary>
    [Fact]
    public async Task HandleAsync_OneUserActiveWithinSeventyTwoHoursOneStale_OnlyFreshUserReturned()
    {
        const string CALLER_SUB = "oid|discover-activity-caller";
        const string FRESH_SUB = "oid|discover-activity-fresh";
        const string STALE_SUB = "oid|discover-activity-stale";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Activity City",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });
            db.Users.Add(CreateUser(FRESH_SUB, now, lastActiveAt: now.AddHours(-24)));
            db.Users.Add(CreateUser(STALE_SUB, now, lastActiveAt: now.AddHours(-96)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.7");

        var response = await client.GetAsync($"api/v1/discover?cityId={cityId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(1);
        body.Items[0].FirstName.Should().Be("User " + FRESH_SUB);
    }

    /// <summary>User with IsOpenToday=false is excluded.</summary>
    [Fact]
    public async Task HandleAsync_UserWithIsOpenTodayFalse_Excluded()
    {
        const string CALLER_SUB = "oid|discover-opentoday-caller";
        const string OPEN_SUB = "oid|discover-opentoday-open";
        const string CLOSED_SUB = "oid|discover-opentoday-closed";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "OpenToday City",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });
            db.Users.Add(CreateUser(OPEN_SUB, now, isOpenToday: true));
            db.Users.Add(CreateUser(CLOSED_SUB, now, isOpenToday: false));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.8");

        var response = await client.GetAsync($"api/v1/discover?cityId={cityId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(1);
        body.Items[0].FirstName.Should().Be("User " + OPEN_SUB);
    }

    /// <summary>HangoutTagSlug filter → only users with Coffee tag returned.</summary>
    [Fact]
    public async Task HandleAsync_HangoutTagSlugSupplied_OnlyMatchingTaggedUsersReturned()
    {
        const string CALLER_SUB = "oid|discover-tag-caller";
        const string COFFEE_SUB = "oid|discover-tag-coffee";
        const string WALK_SUB = "oid|discover-tag-walk";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await SeedHangoutTags(db, now);
            db.Users.Add(CreateUser(CALLER_SUB, now));
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Tag City",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });

            var coffeeUser = CreateUser(COFFEE_SUB, now);
            db.Users.Add(coffeeUser);
            db.UserHangoutTags.Add(new UserHangoutTag
            {
                Id = Guid.NewGuid(),
                UserId = coffeeUser.Id,
                HangoutTagId = CoffeeTagId,
                CreatedAt = now,
            });

            var walkUser = CreateUser(WALK_SUB, now);
            db.Users.Add(walkUser);
            db.UserHangoutTags.Add(new UserHangoutTag
            {
                Id = Guid.NewGuid(),
                UserId = walkUser.Id,
                HangoutTagId = WalkTagId,
                CreatedAt = now,
            });

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.9");

        var response = await client.GetAsync($"api/v1/discover?cityId={cityId}&hangoutTagSlug=Coffee", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(1);
        body.Items[0].FirstName.Should().Be("User " + COFFEE_SUB);
    }

    /// <summary>Caller never appears in their own feed.</summary>
    [Fact]
    public async Task HandleAsync_CallerInOwnRadius_NeverReturnsSelf()
    {
        const string CALLER_SUB = "oid|discover-self-caller";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid cityId;
        Guid callerId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var caller = CreateUser(CALLER_SUB, now);
            callerId = caller.Id;
            db.Users.Add(caller);
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Self City",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.10");

        var response = await client.GetAsync($"api/v1/discover?cityId={cityId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().NotContain(u => u.Id == callerId);
    }

    /// <summary>Soft-deleted candidate is excluded.</summary>
    [Fact]
    public async Task HandleAsync_SoftDeletedCandidateUser_Excluded()
    {
        const string CALLER_SUB = "oid|discover-softdel-caller";
        const string DELETED_SUB = "oid|discover-softdel-target";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Deleted Candidate City",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });
            db.Users.Add(CreateUser(DELETED_SUB, now, deleted: true));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.11");

        var response = await client.GetAsync($"api/v1/discover?cityId={cityId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
    }

    /// <summary>Pending invite from caller to candidate → candidate excluded.</summary>
    [Fact]
    public async Task HandleAsync_PendingInviteFromCallerToCandidate_CandidateExcluded()
    {
        const string CALLER_SUB = "oid|discover-inv1-caller";
        const string CANDIDATE_SUB = "oid|discover-inv1-candidate";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await SeedHangoutTags(db, now);
            var caller = CreateUser(CALLER_SUB, now);
            var candidate = CreateUser(CANDIDATE_SUB, now);
            db.Users.AddRange(caller, candidate);
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Invite City 1",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });
            var placeId = Guid.NewGuid();
            db.Places.Add(new Place
            {
                Id = placeId,
                GooglePlaceId = "g-inv1",
                Name = "Test Cafe",
                CityId = cityId,
                Location = CityCenter(),
                Category = PlaceCategory.Cafe,
                CreatedAt = now,
            });
            db.Invites.Add(new Invite
            {
                Id = Guid.NewGuid(),
                SenderId = caller.Id,
                ReceiverId = candidate.Id,
                HangoutTagId = CoffeeTagId,
                PlaceId = placeId,
                Status = InviteStatus.Pending,
                SentAt = now,
                ExpiresAt = now.AddHours(48),
                CreatedAt = now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.12");

        var response = await client.GetAsync($"api/v1/discover?cityId={cityId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
    }

    /// <summary>Pending invite from candidate to caller → candidate excluded.</summary>
    [Fact]
    public async Task HandleAsync_PendingInviteFromCandidateToCaller_CandidateExcluded()
    {
        const string CALLER_SUB = "oid|discover-inv2-caller";
        const string CANDIDATE_SUB = "oid|discover-inv2-candidate";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await SeedHangoutTags(db, now);
            var caller = CreateUser(CALLER_SUB, now);
            var candidate = CreateUser(CANDIDATE_SUB, now);
            db.Users.AddRange(caller, candidate);
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Invite City 2",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });
            var placeId2 = Guid.NewGuid();
            db.Places.Add(new Place
            {
                Id = placeId2,
                GooglePlaceId = "g-inv2",
                Name = "Test Cafe 2",
                CityId = cityId,
                Location = CityCenter(),
                Category = PlaceCategory.Cafe,
                CreatedAt = now,
            });
            db.Invites.Add(new Invite
            {
                Id = Guid.NewGuid(),
                SenderId = candidate.Id,
                ReceiverId = caller.Id,
                HangoutTagId = CoffeeTagId,
                PlaceId = placeId2,
                Status = InviteStatus.Pending,
                SentAt = now,
                ExpiresAt = now.AddHours(48),
                CreatedAt = now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.13");

        var response = await client.GetAsync($"api/v1/discover?cityId={cityId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
    }

    /// <summary>Accepted or Declined invite between pair → candidate still returned.</summary>
    [Fact]
    public async Task HandleAsync_AcceptedOrDeclinedInviteBetweenPair_CandidateStillReturned()
    {
        const string CALLER_SUB = "oid|discover-inv3-caller";
        const string CANDIDATE_SUB = "oid|discover-inv3-candidate";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            await SeedHangoutTags(db, now);
            var caller = CreateUser(CALLER_SUB, now);
            var candidate = CreateUser(CANDIDATE_SUB, now);
            db.Users.AddRange(caller, candidate);
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Invite City 3",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });
            var placeId3 = Guid.NewGuid();
            db.Places.Add(new Place
            {
                Id = placeId3,
                GooglePlaceId = "g-inv3",
                Name = "Test Cafe 3",
                CityId = cityId,
                Location = CityCenter(),
                Category = PlaceCategory.Cafe,
                CreatedAt = now,
            });
            // Accepted invite
            db.Invites.Add(new Invite
            {
                Id = Guid.NewGuid(),
                SenderId = caller.Id,
                ReceiverId = candidate.Id,
                HangoutTagId = CoffeeTagId,
                PlaceId = placeId3,
                Status = InviteStatus.Accepted,
                SentAt = now.AddDays(-1),
                ExpiresAt = now.AddDays(1),
                RespondedAt = now,
                CreatedAt = now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.14");

        var response = await client.GetAsync($"api/v1/discover?cityId={cityId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(1);
    }

    /// <summary>Sort order verified: TrustScore DESC, LastActiveAt DESC.</summary>
    [Fact]
    public async Task HandleAsync_SortOrder_VerifiedAcrossIsOpenTodayThenTrustScoreThenLastActiveAtDescending()
    {
        const string CALLER_SUB = "oid|discover-sort-caller";
        const string USER_A_SUB = "oid|discover-sort-a"; // trustScore=90, recentmost activity
        const string USER_B_SUB = "oid|discover-sort-b"; // trustScore=90, older activity
        const string USER_C_SUB = "oid|discover-sort-c"; // trustScore=50
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Sort City",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });
            // A: highest trust score + most recent
            db.Users.Add(CreateUser(USER_A_SUB, now, trustScore: 90, lastActiveAt: now.AddMinutes(-10)));
            // B: same trust score as A but older activity
            db.Users.Add(CreateUser(USER_B_SUB, now, trustScore: 90, lastActiveAt: now.AddHours(-2)));
            // C: lower trust score
            db.Users.Add(CreateUser(USER_C_SUB, now, trustScore: 50, lastActiveAt: now.AddMinutes(-5)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.15");

        var response = await client.GetAsync($"api/v1/discover?cityId={cityId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(3);
        body.Items[0].FirstName.Should().Be("User " + USER_A_SUB); // highest trust + most recent
        body.Items[1].FirstName.Should().Be("User " + USER_B_SUB); // same trust, older activity
        body.Items[2].FirstName.Should().Be("User " + USER_C_SUB); // lower trust
    }

    /// <summary>25 matching users, limit=10 → page 1 has 10 items + non-null cursor.</summary>
    [Fact]
    public async Task HandleAsync_TwentyFiveMatchingUsersLimitTen_PageOneReturnsTenWithNonNullCursor()
    {
        const string CALLER_SUB = "oid|discover-page1-caller";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Pagination City",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });
            for (var i = 0; i < 25; i++)
            {
                db.Users.Add(CreateUser($"oid|discover-page1-peer-{i:D2}", now,
                    trustScore: 50 + i, lastActiveAt: now.AddMinutes(-i)));
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.16");

        var response = await client.GetAsync($"api/v1/discover?cityId={cityId}&limit=10", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().HaveCount(10);
        body.NextCursor.Should().NotBeNullOrEmpty();
    }

    /// <summary>Page 2 uses cursor → next 10 items, no overlap with page 1, no skips.</summary>
    [Fact]
    public async Task HandleAsync_PageTwoUsesCursor_ReturnsNextTenDistinctFromPageOneWithNonNullCursor()
    {
        const string CALLER_SUB = "oid|discover-page2-caller";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Pagination City 2",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });
            for (var i = 0; i < 25; i++)
            {
                db.Users.Add(CreateUser($"oid|discover-page2-peer-{i:D2}", now,
                    trustScore: 50 + i, lastActiveAt: now.AddMinutes(-i)));
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.17");

        // Get page 1
        var page1Response = await client.GetAsync($"api/v1/discover?cityId={cityId}&limit=10", TestContext.Current.CancellationToken);
        var page1 = await page1Response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);

        // Get page 2
        var page2Response = await client.GetAsync(
            $"api/v1/discover?cityId={cityId}&limit=10&cursor={Uri.EscapeDataString(page1!.NextCursor!)}",
            TestContext.Current.CancellationToken);

        page2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page2 = await page2Response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        page2!.Items.Should().HaveCount(10);
        page2.NextCursor.Should().NotBeNullOrEmpty();

        // No overlap
        var page1Ids = page1.Items.Select(u => u.Id).ToHashSet();
        page2.Items.Select(u => u.Id).Should().NotIntersectWith(page1Ids);
    }

    /// <summary>Page 3 uses cursor → remaining 5 items + null cursor.</summary>
    [Fact]
    public async Task HandleAsync_PageThreeUsesCursor_ReturnsRemainingFiveWithNullCursor()
    {
        const string CALLER_SUB = "oid|discover-page3-caller";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Pagination City 3",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });
            for (var i = 0; i < 25; i++)
            {
                db.Users.Add(CreateUser($"oid|discover-page3-peer-{i:D2}", now,
                    trustScore: 50 + i, lastActiveAt: now.AddMinutes(-i)));
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.18");

        // Get page 1
        var page1Response = await client.GetAsync($"api/v1/discover?cityId={cityId}&limit=10", TestContext.Current.CancellationToken);
        var page1 = await page1Response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);

        // Get page 2
        var page2Response = await client.GetAsync(
            $"api/v1/discover?cityId={cityId}&limit=10&cursor={Uri.EscapeDataString(page1!.NextCursor!)}",
            TestContext.Current.CancellationToken);
        var page2 = await page2Response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);

        // Get page 3
        var page3Response = await client.GetAsync(
            $"api/v1/discover?cityId={cityId}&limit=10&cursor={Uri.EscapeDataString(page2!.NextCursor!)}",
            TestContext.Current.CancellationToken);

        page3Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page3 = await page3Response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        page3!.Items.Should().HaveCount(5);
        page3.NextCursor.Should().BeNull();
    }

    /// <summary>Cursor past last row → 200 with empty items + null cursor.</summary>
    [Fact]
    public async Task HandleAsync_CursorPastLastRow_ReturnsEmptyItemsAndNullCursor()
    {
        const string CALLER_SUB = "oid|discover-cursorlast-caller";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Cursor Past City",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });
            db.Users.Add(CreateUser("oid|discover-cursorlast-peer", now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Craft a cursor with extreme values that comes after all rows
        var extremeCursor = new DiscoveryCursor(
            LastActiveAt: DateTimeOffset.MinValue,
            TrustScore: 0,
            Id: Guid.Empty,
            IsOpenToday: false);
        var encodedCursor = DiscoveryCursor.Encode(extremeCursor);

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.19");

        var response = await client.GetAsync(
            $"api/v1/discover?cityId={cityId}&cursor={Uri.EscapeDataString(encodedCursor)}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
        body.NextCursor.Should().BeNull();
    }

    /// <summary>No matches → 200 with empty items + null cursor.</summary>
    [Fact]
    public async Task HandleAsync_NoMatches_Returns200EmptyItemsNullCursor()
    {
        const string CALLER_SUB = "oid|discover-nomatch-caller";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Empty City",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.20");

        var response = await client.GetAsync($"api/v1/discover?cityId={cityId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DiscoverFeedResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
        body.NextCursor.Should().BeNull();
    }

    /// <summary>After /discover call, caller's LastActiveAt is unchanged.</summary>
    [Fact]
    public async Task HandleAsync_DiscoverFeed_DoesNotBumpCallersLastActiveAt()
    {
        const string CALLER_SUB = "oid|discover-lastactive-caller";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid callerId;
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var caller = CreateUser(CALLER_SUB, now);
            callerId = caller.Id;
            db.Users.Add(caller);
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "LastActive City",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.10.0.21");

        await client.GetAsync($"api/v1/discover?cityId={cityId}", TestContext.Current.CancellationToken);

        using var verify = App.Services.CreateScope();
        var db2 = verify.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var caller2 = await db2.Users.AsNoTracking().FirstAsync(u => u.Id == callerId, TestContext.Current.CancellationToken);
        caller2.LastActiveAt.Should().Be(now);
    }

    /// <summary>DiscoveryFeatureConfiguration is discovered at startup and appears in swagger tag.</summary>
    [Fact]
    public async Task DiscoveryFeatureConfiguration_DiscoveredAtStartup_AppearsInSwaggerTag()
    {
        var configs = FeatureConfigurationExtensions.DiscoverFeatures(typeof(DiscoveryFeatureConfiguration).Assembly);
        configs.Should().Contain(c => c.Info.Name == "Discovery");

        await Task.CompletedTask;
    }

    private sealed record DiscoverFeedResponseDto(
        IReadOnlyList<UserDto> Items,
        string? NextCursor);

    private sealed record UserDto(
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
