using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using WanderMeet.Api.Features.Auth.RefreshToken;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Auth.RefreshToken;

/// <summary>Integration tests for POST /api/v1/auth/refresh.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class RefreshEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    /// <summary>Happy path: valid refresh token returns 200 with access and refresh tokens.</summary>
    [Fact]
    public async Task HandleAsync_ValidRefreshToken_Returns200WithAccessAndRefreshTokens()
    {
        const string fakeAccessToken = "fake-access-token-value";
        const string fakeRefreshToken = "fake-refresh-token-value";

        var b2cResponsePayload = JsonSerializer.Serialize(new
        {
            access_token = fakeAccessToken,
            refresh_token = fakeRefreshToken,
        });

        var fakeHandler = new FakeB2CHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(b2cResponsePayload, Encoding.UTF8, "application/json"),
            });

        // Create a client pointing at the test host with the B2C handler swapped
        using var client = App.CreateClientWithB2CHandler(fakeHandler);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.10.1");

        var response = await client.PostAsJsonAsync(
            "api/v1/auth/refresh",
            new { RefreshToken = "valid-refresh-token" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<RefreshResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.AccessToken.Should().Be(fakeAccessToken);
        body.RefreshToken.Should().Be(fakeRefreshToken);
    }

    /// <summary>Rate limit exceeded (11 calls) → last call returns 429.</summary>
    [Fact]
    public async Task HandleAsync_RateLimitExceeded_Returns429()
    {
        const string rateLimitTestIp = "10.0.11.1";

        var b2cResponsePayload = JsonSerializer.Serialize(new
        {
            access_token = "some-access-token",
            refresh_token = "some-new-refresh-token",
        });

        var fakeHandler = new FakeB2CHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(b2cResponsePayload, Encoding.UTF8, "application/json"),
            });

        // Reuse the same client so all calls share the same rate-limit counter
        using var client = App.CreateClientWithB2CHandler(fakeHandler);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", rateLimitTestIp);

        HttpResponseMessage? lastResponse = null;

        for (var i = 0; i <= 10; i++)
        {
            lastResponse = await client.PostAsJsonAsync(
                "api/v1/auth/refresh",
                new { RefreshToken = $"refresh-token-{i}" },
                TestContext.Current.CancellationToken);
        }

        lastResponse.Should().NotBeNull();
        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    /// <summary>Fake handler that always returns the provided response.</summary>
    private sealed class FakeB2CHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
