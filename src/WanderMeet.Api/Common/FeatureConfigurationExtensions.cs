using System.Reflection;

namespace WanderMeet.Api.Common;

/// <summary>Auto-discovery for <see cref="IFeatureConfiguration"/> implementations.</summary>
public static class FeatureConfigurationExtensions
{
    /// <summary>
    /// Scans the assembly containing <typeparamref name="TAssemblyMarker"/> for every
    /// non-abstract <see cref="IFeatureConfiguration"/>, instantiates it via its
    /// parameterless ctor, and calls <see cref="IFeatureConfiguration.AddFeatureDependencies"/>.
    /// </summary>
    public static IServiceCollection AddVerticalSliceFeatures<TAssemblyMarker>(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var featureType = typeof(IFeatureConfiguration);
        var implementations = typeof(TAssemblyMarker).Assembly
            .GetTypes()
            .Where(t => featureType.IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false });

        foreach (var implementation in implementations)
        {
            var feature = (IFeatureConfiguration)Activator.CreateInstance(implementation)!;
            feature.AddFeatureDependencies(services, configuration);
        }

        return services;
    }

    /// <summary>Returns every discovered <see cref="IFeatureConfiguration"/> in the given assembly.</summary>
    public static IReadOnlyList<IFeatureConfiguration> DiscoverFeatures(Assembly assembly)
    {
        var featureType = typeof(IFeatureConfiguration);
        return assembly.GetTypes()
            .Where(t => featureType.IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })
            .Select(t => (IFeatureConfiguration)Activator.CreateInstance(t)!)
            .ToList();
    }
}
