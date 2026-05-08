using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Places.ListPlaces;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Places.ListPlaces;

/// <summary>Integration tests for GET /api/v1/places.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class ListPlacesEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private const int Srid = 4326;
    private const double TestLon = 14.42;
    private const double TestLat = 50.08;

    private static Point MakePoint(double lon, double lat) => new(lon, lat) { SRID = Srid };

    private static City CreateCity() => new()
    {
        Id = Guid.NewGuid(),
        Name = "ListCity",
        Country = "CZ",
        Location = MakePoint(TestLon, TestLat),
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static Place CreatePlace(Guid cityId, string name, PlaceCategory category, bool deleted = false) => new()
    {
        Id = Guid.NewGuid(),
        GooglePlaceId = $"gp_{Guid.NewGuid():N}",
        Name = name,
        CityId = cityId,
        Location = MakePoint(TestLon + 0.001, TestLat + 0.001),
        Category = category,
        HasWifi = false,
        IsQuiet = false,
        IsSoloFriendly = false,
        GoogleRating = null,
        WanderMeetupCount = 0,
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
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.51.0.1");

        var response = await client.GetAsync($"api/v1/places?cityId={Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Soft-deleted places are excluded from the list.</summary>
    [Fact]
    public async Task HandleAsync_SoftDeletedPlaces_AreExcluded()
    {
        var city = CreateCity();
        var active = CreatePlace(city.Id, "Active Cafe", PlaceCategory.Cafe);
        var deleted = CreatePlace(city.Id, "Deleted Cafe", PlaceCategory.Cafe, deleted: true);

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        db.Cities.Add(city);
        db.Places.AddRange(active, deleted);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string Sub = "oid|listplaces-softdel";
        var client = App.CreateAuthenticatedClient(Sub);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.51.0.2");

        var response = await client.GetAsync($"api/v1/places?cityId={city.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ListPlacesResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(active.Id);
    }

    /// <summary>All places in city returned alphabetically.</summary>
    [Fact]
    public async Task HandleAsync_MultiplePlaces_ReturnedAlphabetically()
    {
        var city = CreateCity();
        var beta = CreatePlace(city.Id, "Beta Place", PlaceCategory.Restaurant);
        var alpha = CreatePlace(city.Id, "Alpha Place", PlaceCategory.Cafe);
        var gamma = CreatePlace(city.Id, "Gamma Place", PlaceCategory.Park);

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        db.Cities.Add(city);
        db.Places.AddRange(beta, alpha, gamma);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string Sub = "oid|listplaces-order";
        var client = App.CreateAuthenticatedClient(Sub);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.51.0.3");

        var response = await client.GetAsync($"api/v1/places?cityId={city.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ListPlacesResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Items.Select(i => i.Name).Should().BeInAscendingOrder();
        result.Items.Should().HaveCount(3);
    }

    /// <summary>Category filter returns only matching category.</summary>
    [Fact]
    public async Task HandleAsync_CategoryFilter_ReturnsOnlyMatchingCategory()
    {
        var city = CreateCity();
        var cafe = CreatePlace(city.Id, "The Cafe", PlaceCategory.Cafe);
        var restaurant = CreatePlace(city.Id, "The Restaurant", PlaceCategory.Restaurant);

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        db.Cities.Add(city);
        db.Places.AddRange(cafe, restaurant);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string Sub = "oid|listplaces-cat";
        var client = App.CreateAuthenticatedClient(Sub);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.51.0.4");

        var response = await client.GetAsync($"api/v1/places?cityId={city.Id}&category=Cafe", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ListPlacesResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].Category.Should().Be(PlaceCategory.Cafe);
    }
}
