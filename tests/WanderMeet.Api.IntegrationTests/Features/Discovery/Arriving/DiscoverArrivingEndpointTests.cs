using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Discovery.Arriving;

/// <summary>Integration tests for GET /api/v1/discover/arriving.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class DiscoverArrivingEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private const int Srid = 4326;
    private const double CityLon = 14.42;
    private const double CityLat = 50.08;

    private static NetTopologySuite.Geometries.Point CityCenter()
        => new(CityLon, CityLat) { SRID = Srid };

    private static User CreateUser(string sub, DateTimeOffset now, bool deleted = false) => new()
    {
        Id = Guid.NewGuid(),
        AzureAdB2CId = sub,
        FirstName = "User " + sub,
        CreatedAt = now,
        LastActiveAt = now,
        IsOpenToday = true,
        TrustScore = 50,
        DeletedAt = deleted ? now : null,
    };

    private static City CreateCity(Guid id, DateTimeOffset now, bool deleted = false) => new()
    {
        Id = id,
        Name = "Arriving City",
        Country = "CZ",
        Location = CityCenter(),
        CreatedAt = now,
        DeletedAt = deleted ? now : null,
    };

    private static UserCity CreateUserCity(Guid userId, Guid cityId, DateTimeOffset arrivedAt,
        DateTimeOffset? departedAt, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        CityId = cityId,
        ArrivedAt = arrivedAt,
        DepartedAt = departedAt,
        CreatedAt = now,
    };

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.0.1");

        var response = await client.GetAsync($"api/v1/discover/arriving?cityId={Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>JWT sub has no User row → 404 + User.NotRegistered.</summary>
    [Fact]
    public async Task HandleAsync_JwtSubHasNoUserRow_Returns404WithUserNotRegistered()
    {
        const string SUB = "oid|arriving-no-user";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.0.2");

        var response = await client.GetAsync($"api/v1/discover/arriving?cityId={Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.User.NotRegistered);
    }

    /// <summary>Unknown cityId → 404 + Discovery.CityNotFound.</summary>
    [Fact]
    public async Task HandleAsync_UnknownCityId_Returns404WithDiscoveryCityNotFound()
    {
        const string SUB = "oid|arriving-unknown-city";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(SUB, now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.0.3");

        var response = await client.GetAsync($"api/v1/discover/arriving?cityId={Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Discovery.CityNotFound);
    }

    /// <summary>Soft-deleted cityId → 404 + Discovery.CityNotFound.</summary>
    [Fact]
    public async Task HandleAsync_SoftDeletedCityId_Returns404WithDiscoveryCityNotFound()
    {
        const string SUB = "oid|arriving-deleted-city";
        var now = App.FakeTimeProvider.GetUtcNow();
        var cityId = Guid.NewGuid();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(SUB, now));
            db.Cities.Add(CreateCity(cityId, now, deleted: true));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.0.4");

        var response = await client.GetAsync($"api/v1/discover/arriving?cityId={cityId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Discovery.CityNotFound);
    }

    /// <summary>Happy path: returns users arriving in the next 30 days with DepartedAt == null, ordered by ArrivedAt asc.</summary>
    [Fact]
    public async Task HandleAsync_HappyPath_ReturnsUsersWithUserCityArrivedAtInNextThirtyDaysAndDepartedAtNull()
    {
        const string CALLER_SUB = "oid|arriving-happy-caller";
        const string PEER1_SUB = "oid|arriving-happy-peer1";
        const string PEER2_SUB = "oid|arriving-happy-peer2";
        var now = App.FakeTimeProvider.GetUtcNow();
        var cityId = Guid.NewGuid();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            db.Cities.Add(CreateCity(cityId, now));

            // Peer 1 arrives in 5 days
            var peer1 = CreateUser(PEER1_SUB, now);
            db.Users.Add(peer1);
            db.UserCities.Add(CreateUserCity(peer1.Id, cityId, now.AddDays(5), null, now));

            // Peer 2 arrives in 15 days
            var peer2 = CreateUser(PEER2_SUB, now);
            db.Users.Add(peer2);
            db.UserCities.Add(CreateUserCity(peer2.Id, cityId, now.AddDays(15), null, now));

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.0.5");

        var response = await client.GetAsync($"api/v1/discover/arriving?cityId={cityId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ArrivingResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(2);
        // Ordered by ArrivedAt ascending
        body.Items[0].User.FirstName.Should().Be("User " + PEER1_SUB);
        body.Items[1].User.FirstName.Should().Be("User " + PEER2_SUB);
    }

    /// <summary>UserCity.ArrivedAt in the past → excluded.</summary>
    [Fact]
    public async Task HandleAsync_UserCityArrivedAtInPast_Excluded()
    {
        const string CALLER_SUB = "oid|arriving-past-caller";
        const string PEER_SUB = "oid|arriving-past-peer";
        var now = App.FakeTimeProvider.GetUtcNow();
        var cityId = Guid.NewGuid();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            db.Cities.Add(CreateCity(cityId, now));
            var peer = CreateUser(PEER_SUB, now);
            db.Users.Add(peer);
            // ArrivedAt in the past
            db.UserCities.Add(CreateUserCity(peer.Id, cityId, now.AddDays(-1), null, now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.0.6");

        var response = await client.GetAsync($"api/v1/discover/arriving?cityId={cityId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ArrivingResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
    }

    /// <summary>UserCity.ArrivedAt beyond 30 days → excluded.</summary>
    [Fact]
    public async Task HandleAsync_UserCityArrivedAtBeyondThirtyDays_Excluded()
    {
        const string CALLER_SUB = "oid|arriving-beyond-caller";
        const string PEER_SUB = "oid|arriving-beyond-peer";
        var now = App.FakeTimeProvider.GetUtcNow();
        var cityId = Guid.NewGuid();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            db.Cities.Add(CreateCity(cityId, now));
            var peer = CreateUser(PEER_SUB, now);
            db.Users.Add(peer);
            // ArrivedAt 60 days out
            db.UserCities.Add(CreateUserCity(peer.Id, cityId, now.AddDays(60), null, now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.0.7");

        var response = await client.GetAsync($"api/v1/discover/arriving?cityId={cityId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ArrivingResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
    }

    /// <summary>UserCity.DepartedAt is not null → excluded.</summary>
    [Fact]
    public async Task HandleAsync_UserCityWithDepartedAtNotNull_Excluded()
    {
        const string CALLER_SUB = "oid|arriving-departed-caller";
        const string PEER_SUB = "oid|arriving-departed-peer";
        var now = App.FakeTimeProvider.GetUtcNow();
        var cityId = Guid.NewGuid();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            db.Cities.Add(CreateCity(cityId, now));
            var peer = CreateUser(PEER_SUB, now);
            db.Users.Add(peer);
            // DepartedAt is set (already left)
            db.UserCities.Add(CreateUserCity(peer.Id, cityId, now.AddDays(5), now.AddDays(10), now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.0.8");

        var response = await client.GetAsync($"api/v1/discover/arriving?cityId={cityId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ArrivingResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
    }

    /// <summary>Owner is caller → excluded from their own arriving list.</summary>
    [Fact]
    public async Task HandleAsync_OwnerIsCaller_Excluded()
    {
        const string CALLER_SUB = "oid|arriving-self-caller";
        var now = App.FakeTimeProvider.GetUtcNow();
        var cityId = Guid.NewGuid();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var caller = CreateUser(CALLER_SUB, now);
            db.Users.Add(caller);
            db.Cities.Add(CreateCity(cityId, now));
            db.UserCities.Add(CreateUserCity(caller.Id, cityId, now.AddDays(5), null, now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.0.9");

        var response = await client.GetAsync($"api/v1/discover/arriving?cityId={cityId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ArrivingResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
    }

    /// <summary>Owner is soft-deleted → excluded.</summary>
    [Fact]
    public async Task HandleAsync_OwnerIsSoftDeleted_Excluded()
    {
        const string CALLER_SUB = "oid|arriving-softdel-caller";
        const string PEER_SUB = "oid|arriving-softdel-peer";
        var now = App.FakeTimeProvider.GetUtcNow();
        var cityId = Guid.NewGuid();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            db.Cities.Add(CreateCity(cityId, now));
            var peer = CreateUser(PEER_SUB, now, deleted: true);
            db.Users.Add(peer);
            db.UserCities.Add(CreateUserCity(peer.Id, cityId, now.AddDays(5), null, now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.0.10");

        var response = await client.GetAsync($"api/v1/discover/arriving?cityId={cityId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ArrivingResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
    }

    /// <summary>No matching UserCity rows → 200 with empty items.</summary>
    [Fact]
    public async Task HandleAsync_NoMatches_Returns200EmptyItems()
    {
        const string CALLER_SUB = "oid|arriving-nomatch-caller";
        var now = App.FakeTimeProvider.GetUtcNow();
        var cityId = Guid.NewGuid();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(CreateUser(CALLER_SUB, now));
            db.Cities.Add(CreateCity(cityId, now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.0.11");

        var response = await client.GetAsync($"api/v1/discover/arriving?cityId={cityId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ArrivingResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().BeEmpty();
    }

    /// <summary>After arriving call, caller's LastActiveAt is unchanged.</summary>
    [Fact]
    public async Task HandleAsync_ArrivingFeed_DoesNotBumpCallersLastActiveAt()
    {
        const string CALLER_SUB = "oid|arriving-lastactive-caller";
        var now = App.FakeTimeProvider.GetUtcNow();
        var cityId = Guid.NewGuid();
        Guid callerId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var caller = CreateUser(CALLER_SUB, now);
            callerId = caller.Id;
            db.Users.Add(caller);
            db.Cities.Add(CreateCity(cityId, now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.0.12");

        await client.GetAsync($"api/v1/discover/arriving?cityId={cityId}", TestContext.Current.CancellationToken);

        using var verify = App.Services.CreateScope();
        var db2 = verify.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var caller2 = await db2.Users.AsNoTracking()
            .FirstAsync(u => u.Id == callerId, TestContext.Current.CancellationToken);
        caller2.LastActiveAt.Should().Be(now);
    }

    // Local DTOs to deserialize the response
    private sealed record ArrivingResponseDto(IReadOnlyList<ArrivingItemDto> Items);
    private sealed record ArrivingItemDto(UserDto User, DateTimeOffset ArrivingAt);
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
