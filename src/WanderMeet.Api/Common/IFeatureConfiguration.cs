namespace WanderMeet.Api.Common;

/// <summary>
/// Marker for a vertical-slice feature. Auto-discovered at startup via reflection;
/// missing this interface means the feature is invisible in Swagger.
/// Implementations MUST be <c>internal sealed</c> with a parameterless constructor.
/// </summary>
public interface IFeatureConfiguration
{
    /// <summary>Swagger tag info for this feature.</summary>
    FeatureInfo Info { get; }

    /// <summary>Register feature-scoped services.</summary>
    IServiceCollection AddFeatureDependencies(IServiceCollection services, IConfiguration configuration);
}
