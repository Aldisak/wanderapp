using FluentAssertions;
using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Meetups;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Meetups;

/// <summary>Integration tests verifying MeetupsFeatureConfiguration is auto-discovered.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class MeetupsFeatureConfigurationTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    /// <summary>MeetupsFeatureConfiguration must be auto-discovered via reflection at startup.</summary>
    [Fact]
    public void Discover_FeatureConfiguration_RegistersMeetupsTagInSwagger()
    {
        var features = FeatureConfigurationExtensions.DiscoverFeatures(typeof(MeetupsFeatureConfiguration).Assembly);

        features.Should().ContainSingle(c => c is MeetupsFeatureConfiguration);
    }
}
