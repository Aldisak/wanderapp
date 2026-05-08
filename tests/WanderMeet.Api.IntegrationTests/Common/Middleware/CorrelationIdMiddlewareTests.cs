using FluentAssertions;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Common.Middleware;

/// <summary>Integration tests for the correlation-id middleware.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class CorrelationIdMiddlewareTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private const string HeaderName = "X-Correlation-ID";

    /// <summary>When no correlation id is supplied, the response carries a generated one.</summary>
    [Fact]
    public async Task Health_NoCorrelationIdSupplied_ResponseIncludesGeneratedHeader()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.9.0.1");

        var response = await client.GetAsync("api/v1/health", TestContext.Current.CancellationToken);

        response.Headers.Contains(HeaderName).Should().BeTrue("the middleware must mirror an id back on every response");
        response.Headers.GetValues(HeaderName).Single().Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>When a correlation id is supplied on the request, the response echoes the same value.</summary>
    [Fact]
    public async Task Health_CorrelationIdSupplied_ResponseEchoesSameValue()
    {
        const string supplied = "trace-abc-123";
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.9.0.2");
        client.DefaultRequestHeaders.Add(HeaderName, supplied);

        var response = await client.GetAsync("api/v1/health", TestContext.Current.CancellationToken);

        response.Headers.GetValues(HeaderName).Single().Should().Be(supplied);
    }
}
