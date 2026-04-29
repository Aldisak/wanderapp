using WanderMeet.Api.Common;

namespace WanderMeet.Api.Features.Health;

/// <summary>Feature configuration for the Health slice.</summary>
internal sealed class HealthFeatureConfiguration : IFeatureConfiguration
{
    /// <inheritdoc />
    public FeatureInfo Info => new("Health", "Liveness probe");

    /// <inheritdoc />
    public IServiceCollection AddFeatureDependencies(IServiceCollection services, IConfiguration configuration)
        => services;
}
