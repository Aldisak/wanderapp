using FastEndpoints;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WanderMeet.Api.Common;

namespace WanderMeet.Api.Features.Places;

/// <summary>Feature configuration for the Places slice.</summary>
internal sealed class PlacesFeatureConfiguration : IFeatureConfiguration
{
    /// <summary>Swagger tag info for this feature.</summary>
    public FeatureInfo Info => new("Places", "Place suggestions and listings");

    /// <summary>Register feature-scoped services here.</summary>
    public IServiceCollection AddFeatureDependencies(IServiceCollection services, IConfiguration configuration)
        => services;
}
