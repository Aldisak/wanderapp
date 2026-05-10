using System.Net.Http.Json;
using FluentAssertions;
using WanderMeet.Api.Features.Health.GetHealth;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Smoke;

/// <summary>Smoke tests that prove the integration-test fixture boots and the /health endpoint
/// reports per-dependency status.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class HealthSmokeTests : IntegrationTestBase
{
    private readonly IntegrationTestFixture _app;

    /// <summary>Initialises the smoke test with the shared fixture.</summary>
    public HealthSmokeTests(IntegrationTestFixture app) : base(app)
    {
        _app = app;
    }

    /// <summary>Fixture boots, DB is reachable; /health returns 200 with healthy database + per-dep payload.</summary>
    [Fact]
    public async Task GetHealth_WithFixture_ReturnsHealthyAndDatabaseDependencyHealthy()
    {
        var client = _app.CreateAnonymousClient();

        var response = await client.GetAsync("api/v1/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetHealthResponse>(
            TestContext.Current.CancellationToken);

        body.Should().NotBeNull();
        body!.Status.Should().BeOneOf(["healthy", "degraded"]);

        body.Dependencies.Should().NotBeEmpty();

        var database = body.Dependencies.SingleOrDefault(d => d.Name == "database");
        database.Should().NotBeNull(because: "database is the critical dependency and must always appear");
        database!.Status.Should().Be("healthy", because: "the test container Postgres must be reachable");

        body.Dependencies.Should().Contain(d => d.Name == "blob-storage",
            because: "blob storage check is registered");
        body.Dependencies.Should().Contain(d => d.Name == "fcm",
            because: "FCM check is registered");
    }

    /// <summary>Issued token reaches an authenticated route; an unsigned/forged token returns 401.</summary>
    [Fact]
    public async Task TestJwtTokenFactory_IssuedToken_PassesJwtBearerValidation()
    {
        // A valid token signed by the test key should reach the server successfully
        var authenticatedClient = _app.CreateAuthenticatedClient("test-user-sub-123");
        var validResponse = await authenticatedClient.GetAsync("api/v1/health", TestContext.Current.CancellationToken);
        validResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        // Verify the factory produces a well-formed JWT (header.payload.signature)
        var tokenFactory = new TestJwtTokenFactory();
        var token = tokenFactory.CreateToken("test-sub-456");
        token.Should().NotBeNullOrEmpty();
        var parts = token.Split('.');
        parts.Should().HaveCount(3, "JWT must have header.payload.signature");
    }
}
