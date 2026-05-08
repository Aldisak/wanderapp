using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Places.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Places.GetPlace;

/// <summary>Integration tests for GET /api/v1/places/{id}.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class GetPlaceEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private const int Srid = 4326;
    private const double TestLon = 14.42;
    private const double TestLat = 50.08;

    private static Point MakePoint(double lon, double lat) => new(lon, lat) { SRID = Srid };

    private static City CreateCity() => new()
    {
        Id = Guid.NewGuid(),
        Name = "TestCity",
        Country = "CZ",
        Location = MakePoint(TestLon, TestLat),
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static Place CreatePlace(Guid cityId, bool deleted = false) => new()
    {
        Id = Guid.NewGuid(),
        GooglePlaceId = $"gp_{Guid.NewGuid():N}",
        Name = "Test Cafe",
        CityId = cityId,
        Location = MakePoint(TestLon + 0.001, TestLat + 0.001),
        Category = PlaceCategory.Cafe,
        HasWifi = true,
        IsQuiet = false,
        IsSoloFriendly = true,
        GoogleRating = 4.5m,
        WanderMeetupCount = 10,
        IsSponsored = false,
        SponsorPerk = null,
        CreatedAt = DateTimeOffset.UtcNow,
        DeletedAt = deleted ? DateTimeOffset.UtcNow : null,
    };

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearer_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.0.1");

        var response = await client.GetAsync($"api/v1/places/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Unknown id → 404 with Place.NotFound.</summary>
    [Fact]
    public async Task HandleAsync_UnknownId_Returns404WithPlaceNotFound()
    {
        const string Sub = "oid|getplace-unknown";
        var client = App.CreateAuthenticatedClient(Sub);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.0.2");

        var response = await client.GetAsync($"api/v1/places/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Place.NotFound);
    }

    /// <summary>Soft-deleted place → 404 with Place.NotFound.</summary>
    [Fact]
    public async Task HandleAsync_SoftDeletedPlace_Returns404WithPlaceNotFound()
    {
        var city = CreateCity();
        var place = CreatePlace(city.Id, deleted: true);

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        db.Cities.Add(city);
        db.Places.Add(place);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string Sub = "oid|getplace-deleted";
        var client = App.CreateAuthenticatedClient(Sub);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.0.3");

        var response = await client.GetAsync($"api/v1/places/{place.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Place.NotFound);
    }

    /// <summary>Known place → 200 with all fields.</summary>
    [Fact]
    public async Task HandleAsync_KnownPlace_Returns200WithAllFields()
    {
        var city = CreateCity();
        var place = CreatePlace(city.Id);

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        db.Cities.Add(city);
        db.Places.Add(place);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string Sub = "oid|getplace-known";
        var client = App.CreateAuthenticatedClient(Sub);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.0.4");

        var response = await client.GetAsync($"api/v1/places/{place.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<PlaceDto>(TestContext.Current.CancellationToken);
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(place.Id);
        dto.Name.Should().Be("Test Cafe");
        dto.CityId.Should().Be(city.Id);
        dto.Latitude.Should().BeApproximately(TestLat + 0.001, 0.0001);
        dto.Longitude.Should().BeApproximately(TestLon + 0.001, 0.0001);
        dto.Category.Should().Be(PlaceCategory.Cafe);
        dto.HasWifi.Should().BeTrue();
        dto.IsQuiet.Should().BeFalse();
        dto.IsSoloFriendly.Should().BeTrue();
        dto.GoogleRating.Should().Be(4.5m);
        dto.WanderMeetupCount.Should().Be(10);
        dto.IsSponsored.Should().BeFalse();
        dto.SponsorPerk.Should().BeNull();
    }
}
