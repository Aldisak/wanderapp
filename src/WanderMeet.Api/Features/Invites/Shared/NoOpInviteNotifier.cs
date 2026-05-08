using WanderMeet.Api.Database.Entities;

namespace WanderMeet.Api.Features.Invites.Shared;

/// <summary>
/// Phase 3a no-op implementation of <see cref="IInviteNotifier"/>.
/// Logs a Debug-level line for each lifecycle event and does nothing else.
/// Never throws — safe to replace with a real push implementation in Phase 3b.
/// </summary>
internal sealed class NoOpInviteNotifier(ILogger<NoOpInviteNotifier> logger) : IInviteNotifier
{
    /// <inheritdoc />
    public Task InviteSentAsync(Invite invite, CancellationToken ct)
    {
        logger.LogDebug("Invite {InviteId} sent (no-op notifier)", invite.Id);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task InviteAcceptedAsync(Invite invite, Guid meetupId, CancellationToken ct)
    {
        logger.LogDebug("Invite {InviteId} accepted — meetup {MeetupId} created (no-op notifier)", invite.Id, meetupId);
        return Task.CompletedTask;
    }
}
