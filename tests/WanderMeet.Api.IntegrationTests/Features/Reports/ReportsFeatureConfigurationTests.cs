using FluentAssertions;
using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Reports;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Reports;

/// <summary>Integration tests verifying ReportsFeatureConfiguration is auto-discovered.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class ReportsFeatureConfigurationTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    /// <summary>ReportsFeatureConfiguration must be auto-discovered via reflection at startup.</summary>
    [Fact]
    public void Discover_FeatureConfiguration_RegistersReportsTagInSwagger()
    {
        var features = FeatureConfigurationExtensions.DiscoverFeatures(typeof(ReportsFeatureConfiguration).Assembly);

        features.Should().ContainSingle(c => c is ReportsFeatureConfiguration);
    }
}
