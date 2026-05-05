using System.Net;
using System.Text;
using FakeItEasy;
using FastEndpoints;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using WanderMeet.Api.Features.Auth;
using WanderMeet.Api.Features.Auth.RefreshToken;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Auth.RefreshToken;

/// <summary>Unit tests for <see cref="RefreshEndpoint"/> failure branches.</summary>
public class RefreshEndpointTests
{
    /// <summary>Returns 503 with Auth.B2CNotConfigured when AzureAdB2C options are blank.</summary>
    [Fact]
    public async Task HandleAsync_AzureAdB2CSectionMissing_Returns503WithAuthB2CNotConfigured()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AzureAdB2COptions
        {
            Instance = "",
            TenantId = "",
            PolicyId = "",
            ClientId = "",
        });

        var httpClientFactory = A.Fake<IHttpClientFactory>();

        var ep = Factory.Create<RefreshEndpoint>(
            ctx => ctx.Request.Method = "POST",
            httpClientFactory,
            options);

        await ep.HandleAsync(new RefreshRequest { RefreshToken = "some-token" }, TestContext.Current.CancellationToken);

        ep.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    /// <summary>Returns 401 when B2C token endpoint returns a non-success status; upstream body is not leaked.</summary>
    [Fact]
    public async Task HandleAsync_B2CTokenEndpointReturnsNonSuccess_Returns401AndDoesNotLeakUpstreamBody()
    {
        const string UPSTREAM_BODY = "upstream-secret-error-details";

        var options = Microsoft.Extensions.Options.Options.Create(new AzureAdB2COptions
        {
            Instance = "https://login.microsoftonline.com",
            TenantId = "test-tenant",
            PolicyId = "B2C_1_signupsignin",
            ClientId = "test-client-id",
        });

        var fakeHandler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(UPSTREAM_BODY, Encoding.UTF8, "application/json"),
            });

        var httpClient = new HttpClient(fakeHandler);
        var httpClientFactory = A.Fake<IHttpClientFactory>();
        A.CallTo(() => httpClientFactory.CreateClient("AzureAdB2C")).Returns(httpClient);

        var ep = Factory.Create<RefreshEndpoint>(
            ctx => ctx.Request.Method = "POST",
            httpClientFactory,
            options);

        await ep.HandleAsync(new RefreshRequest { RefreshToken = "bad-refresh-token" }, TestContext.Current.CancellationToken);

        ep.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        // Ensure upstream body was not written to the response body
        ep.HttpContext.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
        using var reader = new System.IO.StreamReader(ep.HttpContext.Response.Body);
        var responseBodyText = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        responseBodyText.Should().NotContain(UPSTREAM_BODY);
    }

    /// <summary>Helper: returns a fixed <see cref="HttpResponseMessage"/> for any request.</summary>
    private sealed class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
