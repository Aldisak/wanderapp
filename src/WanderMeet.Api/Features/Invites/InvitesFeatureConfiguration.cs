using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Invites.Realtime;
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
        // SignalRInviteNotifier is Scoped because it consumes WanderMeetDbContext (Scoped).
        services.AddScoped<IInviteNotifier, SignalRInviteNotifier>();
        return services;
    }
}
