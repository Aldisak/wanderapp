using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Users.GetById;

/// <summary>Integration tests for GET /api/v1/users/{id}.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class GetByIdEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    /// <summary>Happy path: existing non-deleted user → 200 with public DTO.</summary>
    [Fact]
    public async Task HandleAsync_UserExists_Returns200WithPublicUserDto()
    {
        const string CALLER_SUB = "oid|getbyid-caller";
        const string TARGET_SUB = "oid|getbyid-target";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid targetId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                AzureAdB2CId = CALLER_SUB,
                FirstName = "Caller",
                CreatedAt = now,
                LastActiveAt = now,
            });

            targetId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = targetId,
                AzureAdB2CId = TARGET_SUB,
                FirstName = "Target",
                Bio = "Public bio",
                IsIdVerified = true,
                TrustScore = 87,
                MeetupCount = 12,
                CitiesCount = 4,
                CreatedAt = now,
                LastActiveAt = now,
            });

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.1.1.1");

        var response = await client.GetAsync($"api/v1/users/{targetId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PublicUserDtoResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.Id.Should().Be(targetId);
        body.FirstName.Should().Be("Target");
        body.Bio.Should().Be("Public bio");
        body.IsIdVerified.Should().BeTrue();
        body.TrustScore.Should().Be(87);
        body.MeetupCount.Should().Be(12);
        body.CitiesCount.Should().Be(4);
    }

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.1.1.2");

        var response = await client.GetAsync($"api/v1/users/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Unknown user id → 404.</summary>
    [Fact]
    public async Task HandleAsync_UnknownId_Returns404()
    {
        const string SUB = "oid|getbyid-unknown-caller";
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
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.1.1.3");

        var response = await client.GetAsync($"api/v1/users/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Soft-deleted target user → 404.</summary>
    [Fact]
    public async Task HandleAsync_SoftDeletedTarget_Returns404()
    {
        const string CALLER_SUB = "oid|getbyid-deleted-caller";
        const string TARGET_SUB = "oid|getbyid-deleted-target";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid targetId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                AzureAdB2CId = CALLER_SUB,
                FirstName = "Caller",
                CreatedAt = now,
                LastActiveAt = now,
            });

            targetId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = targetId,
                AzureAdB2CId = TARGET_SUB,
                FirstName = "Deleted",
                CreatedAt = now,
                LastActiveAt = now,
                DeletedAt = now,
            });

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.1.1.4");

        var response = await client.GetAsync($"api/v1/users/{targetId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record PublicUserDtoResponse(
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
