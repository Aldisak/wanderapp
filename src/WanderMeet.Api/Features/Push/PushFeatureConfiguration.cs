using WanderMeet.Api.Common;
using WanderMeet.Api.Infrastructure.Push;

namespace WanderMeet.Api.Features.Push;

/// <summary>
/// Feature configuration for the Push slice.
/// Owns FCM transport DI registration so that both UC-307 (invite push)
/// and UC-308 (review-prompt push) can consume <see cref="IFcmClient"/>
/// without cross-feature references.
/// </summary>
internal sealed class PushFeatureConfiguration : IFeatureConfiguration
{
    /// <inheritdoc />
    public FeatureInfo Info => new("Push", "FCM push transport for invite/review notifications");

    /// <inheritdoc />
    public IServiceCollection AddFeatureDependencies(IServiceCollection services, IConfiguration configuration)
    {
        // Bind FirebaseOptions from the Firebase configuration section.
        services.AddOptions<FirebaseOptions>()
            .Bind(configuration.GetSection("Firebase"));

        // Choose the correct IFcmClient implementation based on whether a valid credentials file exists.
        var credentialsPath = configuration["Firebase:CredentialsPath"];

        if (!string.IsNullOrEmpty(credentialsPath) && File.Exists(credentialsPath))
        {
            services.AddSingleton<IFirebaseAppInitializer, FirebaseAppInitializer>();
            services.AddSingleton<IFcmClient, FirebaseAdminFcmClient>();
        }
        else
        {
            services.AddSingleton<IFcmClient, NoOpFcmClient>();
        }

        // Register the startup logger unconditionally so the missing-credentials Warning flows
        // through the real ILogger pipeline (Serilog / App Insights) at application startup.
        services.AddHostedService<FirebasePushStartupLogger>();

        return services;
    }
}
