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

namespace WanderMeet.Api.IntegrationTests.Features.Users.UpdateCity;

/// <summary>Integration tests for PATCH /api/v1/users/me/cities/{id}.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class UpdateCityEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private const int Srid = 4326;

    /// <summary>Happy path: own row + valid DepartedAt → 200 + DepartedAt persisted.</summary>
    [Fact]
    public async Task HandleAsync_OwnRowValidDeparted_Returns200AndPersistsDepartedAt()
    {
        const string SUB = "oid|updatecity-happy";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid userCityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var userId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = userId,
                AzureAdB2CId = SUB,
                FirstName = "Alice",
                CreatedAt = now,
                LastActiveAt = now,
            });
            var cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Porto",
                Country = "PT",
                Location = new Point(-8.6291, 41.1579) { SRID = Srid },
                CreatedAt = now,
            });
            userCityId = Guid.NewGuid();
            db.UserCities.Add(new UserCity
            {
                Id = userCityId,
                UserId = userId,
                CityId = cityId,
                ArrivedAt = now.AddDays(-7),
                CreatedAt = now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.2.1.1");

        var departed = now.AddDays(-1);
        var response = await client.PatchAsJsonAsync(
            $"api/v1/users/me/cities/{userCityId}",
            new { DepartedAt = departed },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verify = App.Services.CreateScope();
        var db2 = verify.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var row = await db2.UserCities.AsNoTracking()
            .FirstAsync(uc => uc.Id == userCityId, TestContext.Current.CancellationToken);
        row.DepartedAt.Should().Be(departed);
    }

    /// <summary>No bearer → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearer_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.2.1.2");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/users/me/cities/{Guid.NewGuid()}",
            new { DepartedAt = App.FakeTimeProvider.GetUtcNow() },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Row does not exist or belongs to another user → 404.</summary>
    [Fact]
    public async Task HandleAsync_NotOwnRow_Returns404()
    {
        const string CALLER = "oid|updatecity-foreign-caller";
        const string OWNER = "oid|updatecity-foreign-owner";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid foreignUserCityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                AzureAdB2CId = CALLER,
                FirstName = "Caller",
                CreatedAt = now,
                LastActiveAt = now,
            });
            var ownerId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = ownerId,
                AzureAdB2CId = OWNER,
                FirstName = "Owner",
                CreatedAt = now,
                LastActiveAt = now,
            });
            var cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Madrid",
                Country = "ES",
                Location = new Point(-3.7038, 40.4168) { SRID = Srid },
                CreatedAt = now,
            });
            foreignUserCityId = Guid.NewGuid();
            db.UserCities.Add(new UserCity
            {
                Id = foreignUserCityId,
                UserId = ownerId,
                CityId = cityId,
                ArrivedAt = now.AddDays(-30),
                CreatedAt = now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.2.1.3");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/users/me/cities/{foreignUserCityId}",
            new { DepartedAt = now },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>DepartedAt earlier than ArrivedAt → 400 with DepartedAtBeforeArrived.</summary>
    [Fact]
    public async Task HandleAsync_DepartedBeforeArrived_Returns400WithCode()
    {
        const string SUB = "oid|updatecity-bad-dates";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid userCityId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var userId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = userId,
                AzureAdB2CId = SUB,
                FirstName = "Alice",
                CreatedAt = now,
                LastActiveAt = now,
            });
            var cityId = Guid.NewGuid();
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Barcelona",
                Country = "ES",
                Location = new Point(2.1734, 41.3851) { SRID = Srid },
                CreatedAt = now,
            });
            userCityId = Guid.NewGuid();
            db.UserCities.Add(new UserCity
            {
                Id = userCityId,
                UserId = userId,
                CityId = cityId,
                ArrivedAt = now.AddDays(-2),
                CreatedAt = now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.2.1.4");

        var response = await client.PatchAsJsonAsync(
            $"api/v1/users/me/cities/{userCityId}",
            new { DepartedAt = now.AddDays(-5) },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.UserValidation.DepartedAtBeforeArrived);
    }
}
