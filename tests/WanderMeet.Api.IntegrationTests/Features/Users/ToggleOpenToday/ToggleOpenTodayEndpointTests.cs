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

namespace WanderMeet.Api.IntegrationTests.Features.Users.ToggleOpenToday;

/// <summary>Integration tests for PATCH /api/v1/users/me/open-today.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class ToggleOpenTodayEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    /// <summary>Happy path: toggle IsOpenToday true→false, assert DB row updated.</summary>
    [Fact]
    public async Task HandleAsync_ValidRequest_Returns204AndTogglesIsOpenToday()
    {
        const string SUB = "oid|toggle-happy";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid userId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                AzureAdB2CId = SUB,
                FirstName = "Dave",
                IsOpenToday = true,
                LastActiveAt = now,
                TrustScore = 0,
                MeetupCount = 0,
                CitiesCount = 0,
                CreatedAt = now,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.3.0.1");

        var response = await client.PatchAsJsonAsync(
            "api/v1/users/me/open-today",
            new { IsOpen = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert DB state
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, TestContext.Current.CancellationToken);

            user.Should().NotBeNull();
            user!.IsOpenToday.Should().BeFalse();
            user.LastActiveAt.Should().Be(App.FakeTimeProvider.GetUtcNow());
        }
    }

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.3.0.2");

        var response = await client.PatchAsJsonAsync(
            "api/v1/users/me/open-today",
            new { IsOpen = true },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Valid token but no user row → 404.</summary>
    [Fact]
    public async Task HandleAsync_NoUserRow_Returns404()
    {
        const string SUB = "oid|toggle-no-user";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.3.0.3");

        var response = await client.PatchAsJsonAsync(
            "api/v1/users/me/open-today",
            new { IsOpen = true },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
