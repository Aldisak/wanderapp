using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WanderMeet.Api.Features.Auth.Register;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Auth.Register;

/// <summary>Integration tests for POST /api/v1/auth/register.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class RegisterEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    /// <summary>Happy path: new user registers successfully — 201, body, and DB row are correct.</summary>
    [Fact]
    public async Task HandleAsync_FirstRegistration_Returns201AndPersistsUser()
    {
        const string sub = "oid|test-register-happy";
        var client = App.CreateAuthenticatedClient(sub);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.1.1");
        var expectedNow = App.FakeTimeProvider.GetUtcNow();

        var response = await client.PostAsJsonAsync(
            "api/v1/auth/register",
            new { FirstName = "Alice" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.FirstName.Should().Be("Alice");
        body.IsIdVerified.Should().BeFalse();
        body.TrustScore.Should().Be(0);
        body.CreatedAt.Should().Be(expectedNow);

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.AzureAdB2CId == sub, TestContext.Current.CancellationToken);

        user.Should().NotBeNull();
        user!.FirstName.Should().Be("Alice");
        user.LastActiveAt.Should().Be(expectedNow);
        user.AzureAdB2CId.Should().Be(sub);
    }

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.1.2");

        var response = await client.PostAsJsonAsync(
            "api/v1/auth/register",
            new { FirstName = "Bob" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Duplicate AzureAdB2CId → 409 with Auth.AlreadyRegistered error code.</summary>
    [Fact]
    public async Task HandleAsync_DuplicateAzureAdB2CId_Returns409WithAuthAlreadyRegistered()
    {
        const string sub = "oid|test-register-duplicate";
        var client = App.CreateAuthenticatedClient(sub);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.1.3");

        // First registration
        await client.PostAsJsonAsync(
            "api/v1/auth/register",
            new { FirstName = "Charlie" },
            TestContext.Current.CancellationToken);

        // Second registration with same sub
        var response = await client.PostAsJsonAsync(
            "api/v1/auth/register",
            new { FirstName = "Charlie2" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Auth.AlreadyRegistered);
    }

    /// <summary>Rate limit exceeded (11 calls) → last call returns 429 with Retry-After header.</summary>
    [Fact]
    public async Task HandleAsync_RateLimitExceeded_Returns429WithRetryAfter()
    {
        // Use a dedicated IP for the rate-limit test to isolate it from other tests
        const string rateLimitTestIp = "10.0.2.1";
        HttpResponseMessage? lastResponse = null;

        for (var i = 0; i <= 10; i++)
        {
            // Each call uses a unique sub to avoid 409, allowing us to hit the rate limit
            var callSub = $"oid|test-register-ratelimit-{i}";
            var client = App.CreateAuthenticatedClient(callSub);
            client.DefaultRequestHeaders.Add("X-Forwarded-For", rateLimitTestIp);
            lastResponse = await client.PostAsJsonAsync(
                "api/v1/auth/register",
                new { FirstName = $"User{i}" },
                TestContext.Current.CancellationToken);
        }

        lastResponse.Should().NotBeNull();
        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        lastResponse.Headers.Contains("Retry-After").Should().BeTrue();
    }
}
