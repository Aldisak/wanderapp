using WanderMeet.Api.Database.Entities;

namespace WanderMeet.Api.Features.Invites.Shared;

/// <summary>
/// Abstraction for sending push/real-time notifications on invite lifecycle events.
/// <para>
/// Phase 3a: the default implementation is <see cref="NoOpInviteNotifier"/> (no-op, logs only).
/// Phase 3b: replace the registration in <see cref="InvitesFeatureConfiguration.AddFeatureDependencies"/>
/// with a real push/SignalR implementation once that infrastructure lands.
/// </para>
/// <para>
/// <strong>Callers MUST handle implementation failures locally</strong> (try/catch + LogWarning).
/// A notification failure MUST NOT bubble past the endpoint — persisted state is the source of truth.
/// </para>
/// </summary>
public interface IInviteNotifier
{
    /// <summary>Called after an invite has been persisted with <c>Status = Pending</c>.</summary>
    /// <param name="invite">The newly persisted invite.</param>
    /// <param name="ct">Cancellation token.</param>
    Task InviteSentAsync(Invite invite, CancellationToken ct);

    /// <summary>Called after an invite has been accepted and a meetup row has been persisted.</summary>
    /// <param name="invite">The accepted invite (Status = Accepted).</param>
    /// <param name="meetupId">Id of the newly created meetup row.</param>
    /// <param name="ct">Cancellation token.</param>
    Task InviteAcceptedAsync(Invite invite, Guid meetupId, CancellationToken ct);
}
