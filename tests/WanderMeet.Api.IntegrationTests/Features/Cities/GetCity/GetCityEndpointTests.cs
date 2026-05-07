using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Cities.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Cities.GetCity;

/// <summary>Integration tests for GET /api/v1/cities/{id}.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class GetCityEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private const int Srid = 4326;
    private const double TestLon = 14.42;
    private const double TestLat = 50.08;

    private static Point MakePoint(double lon, double lat) => new(lon, lat) { SRID = Srid };

    private static City CreateCity(Guid id, bool deleted = false) => new()
    {
        Id = id,
        Name = "TestCity",
        Country = "CZ",
        Location = MakePoint(TestLon, TestLat),
        CreatedAt = DateTimeOffset.UtcNow,
        DeletedAt = deleted ? DateTimeOffset.UtcNow : null,
    };

    private static User CreateUser(string sub, Guid cityId, bool isOpenToday, DateTimeOffset lastActiveAt, bool deleted = false)
    {
        var now = DateTimeOffset.UtcNow;
        return new User
        {
            Id = Guid.NewGuid(),
            AzureAdB2CId = sub,
            FirstName = "Nomad",
            CityId = cityId,
            IsOpenToday = isOpenToday,
            LastActiveAt = lastActiveAt,
            TrustScore = 50,
            CreatedAt = now,
            DeletedAt = deleted ? now : null,
        };
    }

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearer_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.40.0.1");

        var response = await client.GetAsync($"api/v1/cities/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Unknown id → 404 with City.NotFound.</summary>
    [Fact]
    public async Task HandleAsync_UnknownId_Returns404WithCityNotFound()
    {
        const string SUB = "oid|getcity-unknown";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.40.0.2");

        var response = await client.GetAsync($"api/v1/cities/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.City.NotFound);
    }

    /// <summary>Soft-deleted city → 404 with City.NotFound.</summary>
    [Fact]
    public async Task HandleAsync_SoftDeletedCity_Returns404WithCityNotFound()
    {
        var cityId = Guid.NewGuid();

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        db.Cities.Add(CreateCity(cityId, deleted: true));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string SUB = "oid|getcity-deleted";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.40.0.3");

        var response = await client.GetAsync($"api/v1/cities/{cityId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.City.NotFound);
    }

    /// <summary>Known city with no active nomads → 200 with correct CityDto fields and ActiveNomadCount=0.</summary>
    [Fact]
    public async Task HandleAsync_KnownCity_Returns200WithDetail()
    {
        var cityId = Guid.NewGuid();

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        db.Cities.Add(CreateCity(cityId));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string SUB = "oid|getcity-known";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.40.0.4");

        var response = await client.GetAsync($"api/v1/cities/{cityId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<CityDetailDto>(TestContext.Current.CancellationToken);
        dto.Should().NotBeNull();
        dto!.City.Id.Should().Be(cityId);
        dto.City.Name.Should().Be("TestCity");
        dto.City.Country.Should().Be("CZ");
        dto.City.Latitude.Should().BeApproximately(TestLat, 0.001);
        dto.City.Longitude.Should().BeApproximately(TestLon, 0.001);
        dto.ActiveNomadCount.Should().Be(0);
    }

    /// <summary>Known city with active nomads → correct ActiveNomadCount (2 open+active, 1 stale excluded).</summary>
    [Fact]
    public async Task HandleAsync_KnownCityWithActiveNomads_ReturnsCorrectCount()
    {
        var cityId = Guid.NewGuid();
        var now = App.FakeTimeProvider.GetUtcNow();
        var recentlyActive = now.Subtract(ValidationConstants.DiscoveryActiveWindow).AddHours(1);
        var stale = now.Subtract(ValidationConstants.DiscoveryActiveWindow).AddHours(-1);

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        db.Cities.Add(CreateCity(cityId));
        // 2 users: open + recently active
        db.Users.Add(CreateUser("oid|nomad1", cityId, isOpenToday: true, lastActiveAt: recentlyActive));
        db.Users.Add(CreateUser("oid|nomad2", cityId, isOpenToday: true, lastActiveAt: recentlyActive));
        // 1 user: stale last active time (should be excluded)
        db.Users.Add(CreateUser("oid|nomad-stale", cityId, isOpenToday: true, lastActiveAt: stale));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string SUB = "oid|getcity-nomads";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.40.0.5");

        var response = await client.GetAsync($"api/v1/cities/{cityId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<CityDetailDto>(TestContext.Current.CancellationToken);
        dto.Should().NotBeNull();
        dto!.ActiveNomadCount.Should().Be(2);
    }
}
