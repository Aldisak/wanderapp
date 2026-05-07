using WanderMeet.Api.Common;
using WanderMeet.Infrastructure.Blob;

namespace WanderMeet.Api.Features.Users;

/// <summary>Feature configuration for the Users slice.</summary>
internal sealed class UsersFeatureConfiguration : IFeatureConfiguration
{
    /// <inheritdoc />
    public FeatureInfo Info => new("Users", "Profile, photos, cities");

    /// <inheritdoc />
    public IServiceCollection AddFeatureDependencies(IServiceCollection services, IConfiguration configuration)
        => services.AddAzureBlobStorage(configuration);
}
