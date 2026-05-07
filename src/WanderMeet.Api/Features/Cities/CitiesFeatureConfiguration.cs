using WanderMeet.Api.Common;

namespace WanderMeet.Api.Features.Cities;

/// <summary>Feature configuration for the Cities slice.</summary>
internal sealed class CitiesFeatureConfiguration : IFeatureConfiguration
{
    /// <inheritdoc />
    public FeatureInfo Info => new("Cities", "City reference data and search");

    /// <inheritdoc />
    public IServiceCollection AddFeatureDependencies(IServiceCollection services, IConfiguration configuration)
        => services;
}
