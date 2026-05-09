using WanderMeet.Api.Common;
using WanderMeet.Api.Infrastructure.Jobs;

namespace WanderMeet.Api.Features.Jobs;

/// <summary>
/// Feature configuration for the Jobs vertical slice.
/// Auto-discovered via reflection by <see cref="FeatureConfigurationExtensions.AddVerticalSliceFeatures{TAssemblyMarker}"/>.
/// </summary>
internal sealed class JobsFeatureConfiguration : IFeatureConfiguration
{
    /// <inheritdoc />
    public FeatureInfo Info => new("Jobs", "Recurring background jobs (invite expiry, review prompt, sink inactive)");

    /// <inheritdoc />
    public IServiceCollection AddFeatureDependencies(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<JobsStartupHostedService>();
        // Scoped job-class registrations (InviteExpiryJob, ReviewPromptJob, SinkInactiveProfilesJob)
        // are added in WI-2.
        return services;
    }
}
