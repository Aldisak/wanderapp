using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Users.GetMe;

/// <summary>Integration tests for GET /api/v1/users/me.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class GetMeEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    /// <summary>Happy path: authenticated user with profile → 200 with correct DTO fields.</summary>
    [Fact]
    public async Task HandleAsync_AuthenticatedUserExists_Returns200WithUserDto()
    {
        const string SUB = "oid|getme-happy";
        var expectedNow = App.FakeTimeProvider.GetUtcNow();

        // Seed the user
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var hangoutTag = new HangoutTag { Id = Guid.NewGuid(), Slug = HangoutTagSlug.Coffee, Label = "Coffee", Emoji = "☕", CreatedAt = expectedNow };
            db.HangoutTags.Add(hangoutTag);
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                AzureAdB2CId = SUB,
                FirstName = "Alice",
                Bio = "Loves travel",
                IsIdVerified = false,
                IsOpenToday = true,
                IsOpenToRomance = false,
                LastActiveAt = expectedNow,
                TrustScore = 42,
                MeetupCount = 3,
                CitiesCount = 5,
                YearsNomading = 2.5m,
                CreatedAt = expectedNow,
                HangoutTags = [new UserHangoutTag { Id = Guid.NewGuid(), UserId = userId, HangoutTagId = hangoutTag.Id, CreatedAt = expectedNow }],
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.1.0.1");

        var response = await client.GetAsync("api/v1/users/me", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserDtoResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.FirstName.Should().Be("Alice");
        body.Bio.Should().Be("Loves travel");
        body.IsOpenToday.Should().BeTrue();
        body.TrustScore.Should().Be(42);
        body.MeetupCount.Should().Be(3);
        body.CitiesCount.Should().Be(5);
        body.HangoutTagIds.Should().HaveCount(1);
    }

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.1.0.2");

        var response = await client.GetAsync("api/v1/users/me", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Valid token but no user row → 404 with User.NotRegistered code.</summary>
    [Fact]
    public async Task HandleAsync_NoUserRow_Returns404WithNotRegisteredCode()
    {
        const string SUB = "oid|getme-no-user";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.1.0.3");

        var response = await client.GetAsync("api/v1/users/me", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.User.NotRegistered);
    }

    // Local DTO for deserialization (mirrors UserDto shape)
    private sealed record UserDtoResponse(
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
