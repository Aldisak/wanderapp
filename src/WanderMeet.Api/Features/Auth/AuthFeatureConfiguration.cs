using WanderMeet.Api.Common;

namespace WanderMeet.Api.Features.Auth;

/// <summary>Feature configuration for the Auth slice.</summary>
internal sealed class AuthFeatureConfiguration : IFeatureConfiguration
{
    /// <inheritdoc />
    public FeatureInfo Info => new("Auth", "Sign-up + token refresh via Azure AD B2C");

    /// <inheritdoc />
    public IServiceCollection AddFeatureDependencies(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<AzureAdB2COptions>()
            .Bind(configuration.GetSection("AzureAdB2C"));

        services.AddHttpClient("AzureAdB2C");

        return services;
    }
}
