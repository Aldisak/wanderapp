using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WanderMeet.Infrastructure.Blob;

/// <summary>Extension methods for registering blob storage services.</summary>
public static class BlobStorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IBlobStorageService"/> as a singleton backed by <see cref="AzureBlobStorageService"/>,
    /// bound from the <c>"BlobStorage"</c> configuration section.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">The application configuration.</param>
    public static IServiceCollection AddAzureBlobStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BlobStorageOptions>()
            .Bind(configuration.GetSection("BlobStorage"));

        services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

        return services;
    }
}
