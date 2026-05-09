using WanderMeet.Api.Common;

namespace WanderMeet.Api.Features.Meetups;

/// <summary>Feature configuration for the Meetups slice.</summary>
internal sealed class MeetupsFeatureConfiguration : IFeatureConfiguration
{
    /// <inheritdoc />
    public FeatureInfo Info => new("Meetups", "Pending-review list and post-meetup review submission");

    /// <inheritdoc />
    public IServiceCollection AddFeatureDependencies(IServiceCollection services, IConfiguration configuration)
        => services;
}
