using WanderMeet.Api.Common;

namespace WanderMeet.Api.Features.Discovery;

/// <summary>Feature configuration for the Discovery slice.</summary>
internal sealed class DiscoveryFeatureConfiguration : IFeatureConfiguration
{
    /// <inheritdoc />
    public FeatureInfo Info => new("Discovery", "Nearby-user feed and arriving-soon list");

    /// <inheritdoc />
    public IServiceCollection AddFeatureDependencies(IServiceCollection services, IConfiguration configuration)
        => services;
}
