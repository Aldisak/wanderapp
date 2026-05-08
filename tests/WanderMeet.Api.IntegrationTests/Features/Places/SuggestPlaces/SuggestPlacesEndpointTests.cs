using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Places.SuggestPlaces;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Places.SuggestPlaces;

/// <summary>Integration tests for GET /api/v1/places/suggest.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class SuggestPlacesEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private const int Srid = 4326;
    private const double CityLon = 14.42;
    private const double CityLat = 50.08;

    private static Point MakePoint(double lon, double lat) => new(lon, lat) { SRID = Srid };

    private static City CreateCity() => new()
    {
        Id = Guid.NewGuid(),
        Name = "SuggestCity",
        Country = "CZ",
        Location = MakePoint(CityLon, CityLat),
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static Place CreatePlace(
        Guid cityId,
        string name,
        PlaceCategory category,
        bool isSponsored = false,
        int meetupCount = 0,
        double lonOffset = 0.001,
        double latOffset = 0.001,
        bool deleted = false) => new()
    {
        Id = Guid.NewGuid(),
        GooglePlaceId = $"gp_{Guid.NewGuid():N}",
        Name = name,
        CityId = cityId,
        Location = MakePoint(CityLon + lonOffset, CityLat + latOffset),
        Category = category,
        HasWifi = false,
        IsQuiet = false,
        IsSoloFriendly = false,
        GoogleRating = null,
        WanderMeetupCount = meetupCount,
        IsSponsored = isSponsored,
        SponsorPerk = isSponsored ? "Free coffee" : null,
        CreatedAt = DateTimeOffset.UtcNow,
        DeletedAt = deleted ? DateTimeOffset.UtcNow : null,
    };

    private static string BuildSuggestUrl(Guid cityId, double lat = CityLat, double lng = CityLon, string? hangoutTagSlug = null)
    {
        var url = $"api/v1/places/suggest?cityId={cityId}&lat={lat}&lng={lng}";
        if (hangoutTagSlug is not null)
            url += $"&hangoutTagSlug={hangoutTagSlug}";
        return url;
    }

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearer_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.52.0.1");

        var response = await client.GetAsync(BuildSuggestUrl(Guid.NewGuid()), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Two regular plus one sponsored → 3 items with sponsored as slot 3.</summary>
    [Fact]
    public async Task HandleAsync_TwoRegularPlusOneSponsored_ReturnsAllThreeWithSponsoredAsSlotThree()
    {
        var city = CreateCity();

        // 5 regular + 1 sponsored
        var regulars = Enumerable.Range(1, 5)
            .Select(i => CreatePlace(city.Id, $"Regular {i}", PlaceCategory.Cafe, isSponsored: false, meetupCount: i))
            .ToArray();
        var sponsored = CreatePlace(city.Id, "Sponsored", PlaceCategory.Cafe, isSponsored: true, meetupCount: 10);

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        db.Cities.Add(city);
        db.Places.AddRange(regulars);
        db.Places.Add(sponsored);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string Sub = "oid|suggest-sponsored";
        var client = App.CreateAuthenticatedClient(Sub);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.52.0.2");

        var response = await client.GetAsync(BuildSuggestUrl(city.Id), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SuggestPlacesResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(3);
        result.Items[2].IsSponsored.Should().BeTrue();
        result.Items[0].IsSponsored.Should().BeFalse();
        result.Items[1].IsSponsored.Should().BeFalse();
    }

    /// <summary>No sponsored places → top 3 regular returned.</summary>
    [Fact]
    public async Task HandleAsync_NoSponsored_TopThreeRegular()
    {
        var city = CreateCity();

        var regulars = Enumerable.Range(1, 5)
            .Select(i => CreatePlace(city.Id, $"Regular {i}", PlaceCategory.Cafe, isSponsored: false, meetupCount: i))
            .ToArray();

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        db.Cities.Add(city);
        db.Places.AddRange(regulars);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string Sub = "oid|suggest-nosp";
        var client = App.CreateAuthenticatedClient(Sub);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.52.0.3");

        var response = await client.GetAsync(BuildSuggestUrl(city.Id), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SuggestPlacesResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(3);
        result.Items.Should().AllSatisfy(p => p.IsSponsored.Should().BeFalse());
    }

    /// <summary>HangoutTagSlug=Coffee → only Cafe places returned.</summary>
    [Fact]
    public async Task HandleAsync_HangoutTagSlugFilter_ConstrainsCategoryMapping()
    {
        var city = CreateCity();

        // Mix of cafes and restaurants
        var cafe1 = CreatePlace(city.Id, "Cafe A", PlaceCategory.Cafe, meetupCount: 5);
        var cafe2 = CreatePlace(city.Id, "Cafe B", PlaceCategory.Cafe, meetupCount: 3);
        var restaurant = CreatePlace(city.Id, "Restaurant", PlaceCategory.Restaurant, meetupCount: 10);

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        db.Cities.Add(city);
        db.Places.AddRange(cafe1, cafe2, restaurant);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string Sub = "oid|suggest-filter";
        var client = App.CreateAuthenticatedClient(Sub);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.52.0.4");

        var response = await client.GetAsync(BuildSuggestUrl(city.Id, hangoutTagSlug: "Coffee"), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SuggestPlacesResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Items.Should().AllSatisfy(p => p.Category.Should().Be(PlaceCategory.Cafe));
    }

    /// <summary>Places ordered by WanderMeetupCount descending then distance ascending.</summary>
    [Fact]
    public async Task HandleAsync_OrdersByMeetupCountDescThenDistanceAsc()
    {
        var city = CreateCity();

        // 4 regular places with controlled meetup counts and locations
        var near = CreatePlace(city.Id, "Near Low", PlaceCategory.Cafe, meetupCount: 1, lonOffset: 0.001, latOffset: 0.001);
        var farHigh = CreatePlace(city.Id, "Far High", PlaceCategory.Cafe, meetupCount: 10, lonOffset: 0.1, latOffset: 0.1);
        var nearHigh = CreatePlace(city.Id, "Near High", PlaceCategory.Cafe, meetupCount: 10, lonOffset: 0.002, latOffset: 0.002);
        var farLow = CreatePlace(city.Id, "Far Low", PlaceCategory.Cafe, meetupCount: 1, lonOffset: 0.2, latOffset: 0.2);

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        db.Cities.Add(city);
        db.Places.AddRange(near, farHigh, nearHigh, farLow);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string Sub = "oid|suggest-order";
        var client = App.CreateAuthenticatedClient(Sub);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.52.0.5");

        var response = await client.GetAsync(BuildSuggestUrl(city.Id), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SuggestPlacesResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        // First item: highest meetup count + nearest among equal = nearHigh (10 meetups, closer than farHigh)
        result!.Items[0].Name.Should().Be("Near High");
        // Second item: farHigh (10 meetups, farther)
        result.Items[1].Name.Should().Be("Far High");
    }

    /// <summary>Only one place in city → returns one item, no errors.</summary>
    [Fact]
    public async Task HandleAsync_OnlyOnePlaceInCity_ReturnsOnePlaceNoErrors()
    {
        var city = CreateCity();
        var place = CreatePlace(city.Id, "Lone Place", PlaceCategory.Cafe);

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        db.Cities.Add(city);
        db.Places.Add(place);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string Sub = "oid|suggest-one";
        var client = App.CreateAuthenticatedClient(Sub);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.52.0.6");

        var response = await client.GetAsync(BuildSuggestUrl(city.Id), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SuggestPlacesResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
    }

    /// <summary>Soft-deleted places are excluded.</summary>
    [Fact]
    public async Task HandleAsync_SoftDeletedPlaces_Excluded()
    {
        var city = CreateCity();
        var active = CreatePlace(city.Id, "Active", PlaceCategory.Cafe);
        var deleted = CreatePlace(city.Id, "Deleted", PlaceCategory.Cafe, deleted: true);

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        db.Cities.Add(city);
        db.Places.AddRange(active, deleted);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string Sub = "oid|suggest-del";
        var client = App.CreateAuthenticatedClient(Sub);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.52.0.7");

        var response = await client.GetAsync(BuildSuggestUrl(city.Id), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SuggestPlacesResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(active.Id);
    }
}
