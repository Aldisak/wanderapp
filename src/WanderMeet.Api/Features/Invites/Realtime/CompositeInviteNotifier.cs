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
        await InvokeChild("SignalR", "InviteSent", invite.Id,
            () => signalRNotifier.InviteSentAsync(invite, ct));
        await InvokeChild("FCM", "InviteSent", invite.Id,
            () => fcmNotifier.InviteSentAsync(invite, ct));
    }

    /// <inheritdoc />
    public async Task InviteAcceptedAsync(Invite invite, Guid meetupId, CancellationToken ct)
    {
        await InvokeChild("SignalR", "InviteAccepted", invite.Id,
            () => signalRNotifier.InviteAcceptedAsync(invite, meetupId, ct));
        await InvokeChild("FCM", "InviteAccepted", invite.Id,
            () => fcmNotifier.InviteAcceptedAsync(invite, meetupId, ct));
    }

    /// <inheritdoc />
    public async Task InviteDeclinedAsync(Invite invite, CancellationToken ct)
    {
        await InvokeChild("SignalR", "InviteDeclined", invite.Id,
            () => signalRNotifier.InviteDeclinedAsync(invite, ct));
        await InvokeChild("FCM", "InviteDeclined", invite.Id,
            () => fcmNotifier.InviteDeclinedAsync(invite, ct));
    }

    /// <inheritdoc />
    public async Task InviteExpiredAsync(Invite invite, CancellationToken ct)
    {
        await InvokeChild("SignalR", "InviteExpired", invite.Id,
            () => signalRNotifier.InviteExpiredAsync(invite, ct));
        await InvokeChild("FCM", "InviteExpired", invite.Id,
            () => fcmNotifier.InviteExpiredAsync(invite, ct));
    }

    /// <summary>
    /// Runs <paramref name="action"/> and logs a Warning with the failing notifier and phase
    /// if it throws. The exception is swallowed — sibling notifiers must still fire.
    /// </summary>
    private async Task InvokeChild(string notifier, string phase, Guid inviteId, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notifier child failed {Notifier} {Phase} {InviteId}", notifier, phase, inviteId);
        }
    }
}
