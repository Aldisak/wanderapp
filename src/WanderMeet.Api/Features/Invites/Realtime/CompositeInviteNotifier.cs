using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Invites.Shared;

namespace WanderMeet.Api.Features.Invites.Realtime;

/// <summary>
/// Fan-out implementation of <see cref="IInviteNotifier"/> that calls both
/// <see cref="SignalRInviteNotifier"/> and <see cref="FcmInviteNotifier"/> in sequence,
/// isolating each child inside its own try/catch so a failure in one never prevents
/// the other from running.
/// </summary>
/// <remarks>
/// This composite takes the CONCRETE <see cref="SignalRInviteNotifier"/> and
/// <see cref="FcmInviteNotifier"/> types — not <see cref="IInviteNotifier"/> — by design.
/// Injecting <c>IEnumerable&lt;IInviteNotifier&gt;</c> would self-recurse because the composite
/// itself is registered as <see cref="IInviteNotifier"/>.
/// Adding a third notifier to the chain requires (a) registering the new concrete type in
/// PushFeatureConfiguration / InvitesFeatureConfiguration, (b) adding a primary-ctor parameter
/// on this class, and (c) adding the per-method try/catch invocation in each of the four
/// <see cref="IInviteNotifier"/> methods below.
/// </remarks>
internal sealed class CompositeInviteNotifier(
    SignalRInviteNotifier signalRNotifier,
    FcmInviteNotifier fcmNotifier,
    ILogger<CompositeInviteNotifier> logger) : IInviteNotifier
{
    /// <inheritdoc />
    public async Task InviteSentAsync(Invite invite, CancellationToken ct)
    {
        try
        {
            await signalRNotifier.InviteSentAsync(invite, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CompositeInviteNotifier] SignalR failed for InviteSent invite {InviteId}", invite.Id);
        }

        try
        {
            await fcmNotifier.InviteSentAsync(invite, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CompositeInviteNotifier] FCM failed for InviteSent invite {InviteId}", invite.Id);
        }
    }

    /// <inheritdoc />
    public async Task InviteAcceptedAsync(Invite invite, Guid meetupId, CancellationToken ct)
    {
        try
        {
            await signalRNotifier.InviteAcceptedAsync(invite, meetupId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CompositeInviteNotifier] SignalR failed for InviteAccepted invite {InviteId}", invite.Id);
        }

        try
        {
            await fcmNotifier.InviteAcceptedAsync(invite, meetupId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CompositeInviteNotifier] FCM failed for InviteAccepted invite {InviteId}", invite.Id);
        }
    }

    /// <inheritdoc />
    public async Task InviteDeclinedAsync(Invite invite, CancellationToken ct)
    {
        try
        {
            await signalRNotifier.InviteDeclinedAsync(invite, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CompositeInviteNotifier] SignalR failed for InviteDeclined invite {InviteId}", invite.Id);
        }

        try
        {
            await fcmNotifier.InviteDeclinedAsync(invite, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CompositeInviteNotifier] FCM failed for InviteDeclined invite {InviteId}", invite.Id);
        }
    }

    /// <inheritdoc />
    public async Task InviteExpiredAsync(Invite invite, CancellationToken ct)
    {
        try
        {
            await signalRNotifier.InviteExpiredAsync(invite, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CompositeInviteNotifier] SignalR failed for InviteExpired invite {InviteId}", invite.Id);
        }

        try
        {
            await fcmNotifier.InviteExpiredAsync(invite, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CompositeInviteNotifier] FCM failed for InviteExpired invite {InviteId}", invite.Id);
        }
    }
}
