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

    /// <summary>Called after an invite has been declined by the receiver. Fires a sender-side notification only; the receiver sees no UI toast (silent on the UI).</summary>
    /// <param name="invite">The declined invite (Status = Declined).</param>
    /// <param name="ct">Cancellation token.</param>
    Task InviteDeclinedAsync(Invite invite, CancellationToken ct);

    /// <summary>Called when a pending invite has expired without a response. Notifies both sender and receiver that the window has closed.</summary>
    /// <param name="invite">The expired invite (Status = Pending, ExpiresAt in the past).</param>
    /// <param name="ct">Cancellation token.</param>
    Task InviteExpiredAsync(Invite invite, CancellationToken ct);
}
