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
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Users.AddCity;

/// <summary>Integration tests for POST /api/v1/users/me/cities.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class AddCityEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private const int Srid = 4326;

    /// <summary>Happy path: known city + arrivedAt in past → 201 + UserCity row + CitiesCount incremented.</summary>
    [Fact]
    public async Task HandleAsync_KnownCity_Returns201AndIncrementsCitiesCount()
    {
        const string SUB = "oid|addcity-happy";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid userId;
        Guid cityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            userId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = userId,
                AzureAdB2CId = SUB,
                FirstName = "Alice",
                CreatedAt = now,
                LastActiveAt = now,
                CitiesCount = 0,
            });
            cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Lisbon",
                Country = "PT",
                Location = new Point(-9.1393, 38.7223) { SRID = Srid },
                CreatedAt = now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.2.0.1");

        var arrived = now.AddDays(-3);
        var response = await client.PostAsJsonAsync(
            "api/v1/users/me/cities",
            new { CityId = cityId, ArrivedAt = arrived },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<UserCityDtoResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.CityId.Should().Be(cityId);
        body.ArrivedAt.Should().Be(arrived);
        body.DepartedAt.Should().BeNull();

        using var verify = App.Services.CreateScope();
        var db2 = verify.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var user = await db2.Users.AsNoTracking()
            .FirstAsync(u => u.Id == userId, TestContext.Current.CancellationToken);
        user.CitiesCount.Should().Be(1);
        user.LastActiveAt.Should().Be(now);
        var rows = await db2.UserCities.AsNoTracking()
            .Where(uc => uc.UserId == userId).ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().ContainSingle(uc => uc.CityId == cityId && uc.DepartedAt == null);
    }

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.2.0.2");

        var response = await client.PostAsJsonAsync(
            "api/v1/users/me/cities",
            new { CityId = Guid.NewGuid(), ArrivedAt = App.FakeTimeProvider.GetUtcNow() },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Authenticated but no user row → 404 with User.NotRegistered.</summary>
    [Fact]
    public async Task HandleAsync_NoUserRow_Returns404WithNotRegistered()
    {
        const string SUB = "oid|addcity-no-user";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.2.0.3");

        var response = await client.PostAsJsonAsync(
            "api/v1/users/me/cities",
            new { CityId = Guid.NewGuid(), ArrivedAt = App.FakeTimeProvider.GetUtcNow().AddDays(-1) },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.User.NotRegistered);
    }

    /// <summary>Unknown cityId → 400 with CityIdNotFound.</summary>
    [Fact]
    public async Task HandleAsync_UnknownCityId_Returns400WithCityIdNotFound()
    {
        const string SUB = "oid|addcity-unknown-city";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                AzureAdB2CId = SUB,
                FirstName = "Caller",
                CreatedAt = now,
                LastActiveAt = now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.2.0.4");

        var response = await client.PostAsJsonAsync(
            "api/v1/users/me/cities",
            new { CityId = Guid.NewGuid(), ArrivedAt = now.AddDays(-1) },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.UserValidation.CityIdNotFound);
    }

    private sealed record UserCityDtoResponse(
        Guid Id,
        Guid CityId,
        DateTimeOffset ArrivedAt,
        DateTimeOffset? DepartedAt);
}
