using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Invites.Shared;

namespace WanderMeet.Api.Features.Invites;

/// <summary>Feature configuration for the Invites slice.</summary>
internal sealed class InvitesFeatureConfiguration : IFeatureConfiguration
{
    /// <inheritdoc />
    public FeatureInfo Info => new("Invites", "Send / accept / decline / list invites between users");

    /// <inheritdoc />
    public IServiceCollection AddFeatureDependencies(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IInviteNotifier, NoOpInviteNotifier>();
        return services;
    }
}
