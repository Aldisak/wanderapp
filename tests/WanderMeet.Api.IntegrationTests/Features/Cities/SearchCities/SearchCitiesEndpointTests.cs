using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Cities.SearchCities;
using WanderMeet.Api.Features.Cities.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Cities.SearchCities;

/// <summary>Integration tests for GET /api/v1/cities/search.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class SearchCitiesEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private const int Srid = 4326;

    private static Point AnyPoint() => new(14.42, 50.08) { SRID = Srid };

    private static City CreateCity(string name, string country = "PT", bool deleted = false) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Country = country,
        Location = AnyPoint(),
        CreatedAt = DateTimeOffset.UtcNow,
        DeletedAt = deleted ? DateTimeOffset.UtcNow : null,
    };

    private static async Task SeedCities(WanderMeetDbContext db, IEnumerable<City> cities)
    {
        db.Cities.AddRange(cities);
        await db.SaveChangesAsync();
    }

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearer_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.0.1");

        var response = await client.GetAsync("api/v1/cities/search?q=lis", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Query matches Lisbon and Lisboa but not Porto → both returned.</summary>
    [Fact]
    public async Task HandleAsync_QueryMatchesCities_Returns200WithMatches()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        await SeedCities(db, [
            CreateCity("Lisbon"),
            CreateCity("Lisboa"),
            CreateCity("Porto"),
        ]);

        const string SUB = "oid|search-cities-matches";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.0.2");

        var response = await client.GetAsync("api/v1/cities/search?q=lis", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SearchCitiesResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
        result.Items.Select(x => x.Name).Should().BeEquivalentTo(["Lisbon", "Lisboa"]);
    }

    /// <summary>No cities match query → 200 with empty list.</summary>
    [Fact]
    public async Task HandleAsync_NoMatches_Returns200WithEmptyList()
    {
        const string SUB = "oid|search-cities-no-match";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.0.3");

        var response = await client.GetAsync("api/v1/cities/search?q=zzz", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SearchCitiesResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
    }

    /// <summary>Soft-deleted city is not returned.</summary>
    [Fact]
    public async Task HandleAsync_SoftDeletedCity_Excluded()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        await SeedCities(db, [
            CreateCity("Deleted City", deleted: true),
            CreateCity("Active City"),
        ]);

        const string SUB = "oid|search-cities-deleted";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.0.4");

        var response = await client.GetAsync("api/v1/cities/search?q=city", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SearchCitiesResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items.Single().Name.Should().Be("Active City");
    }

    /// <summary>Seeding 25 matching cities with limit=10 returns exactly 10.</summary>
    [Fact]
    public async Task HandleAsync_LimitRespected_TopNReturned()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var cities = Enumerable.Range(1, 25).Select(i => CreateCity($"LimitCity{i:D2}")).ToList();
        await SeedCities(db, cities);

        const string SUB = "oid|search-cities-limit";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.0.5");

        var response = await client.GetAsync("api/v1/cities/search?q=limitcity&limit=10", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SearchCitiesResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(10);
    }

    /// <summary>Query shorter than 2 chars → 400 with SearchQueryTooShort.</summary>
    [Fact]
    public async Task HandleAsync_QueryTooShort_Returns400WithSearchQueryTooShort()
    {
        const string SUB = "oid|search-cities-short-query";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.0.6");

        var response = await client.GetAsync("api/v1/cities/search?q=l", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Validation.SearchQueryTooShort);
    }
}
