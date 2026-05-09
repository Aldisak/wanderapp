using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Invites.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.Infrastructure.Push;
using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Features.Invites.Realtime;

/// <summary>Projection record for FCM push on <c>InviteSentAsync</c>.</summary>
internal sealed record FcmInviteSentProjection(
    string? FcmToken,
    bool ReceiverDeleted,
    string SenderFirstName,
    string PlaceName,
    HangoutTagSlug Slug,
    bool SenderIsThere);

/// <summary>Projection record for FCM push on <c>InviteAcceptedAsync</c>.</summary>
internal sealed record FcmInviteAcceptedProjection(
    string? FcmToken,
    bool SenderDeleted,
    bool ReceiverDeleted,
    string ReceiverFirstName,
    string PlaceName);

/// <summary>
/// FCM-backed implementation of <see cref="IInviteNotifier"/>.
/// Sends push notifications via <see cref="IFcmClient"/> on invite lifecycle events.
/// </summary>
internal class FcmInviteNotifier(
    IFcmClient fcmClient,
    WanderMeetDbContext dbContext,
    ILogger<FcmInviteNotifier> logger) : IInviteNotifier
{
    /// <inheritdoc />
    public virtual async Task InviteSentAsync(Invite invite, CancellationToken ct)
    {
        var projection = await dbContext.Invites
            .AsNoTracking()
            .Where(i => i.Id == invite.Id)
            .Select(i => new FcmInviteSentProjection(
                i.Receiver!.FcmToken,
                i.Receiver.DeletedAt != null,
                i.Sender!.FirstName,
                i.Place!.Name,
                i.HangoutTag!.Slug,
                i.SenderIsThere))
            .FirstOrDefaultAsync(ct);

        if (projection is null)
        {
            logger.LogDebug("FcmInviteNotifier: invite {InviteId} not found, skipping FCM push", invite.Id);
            return;
        }

        if (projection.ReceiverDeleted)
        {
            logger.LogDebug("FcmInviteNotifier: invite {InviteId} receiver soft-deleted, skipping FCM", invite.Id);
            return;
        }

        if (string.IsNullOrEmpty(projection.FcmToken))
        {
            logger.LogDebug("FcmInviteNotifier: invite {InviteId} receiver has no FCM token, skipping", invite.Id);
            return;
        }

        var (title, body) = projection.SenderIsThere
            ? PushTemplates.ImThere(projection.SenderFirstName, projection.PlaceName, projection.Slug)
            : PushTemplates.Standard(projection.SenderFirstName, projection.PlaceName, projection.Slug);

        await fcmClient.SendAsync(projection.FcmToken, title, body, ct);
    }

    /// <inheritdoc />
    public virtual async Task InviteAcceptedAsync(Invite invite, Guid meetupId, CancellationToken ct)
    {
        var projection = await dbContext.Invites
            .AsNoTracking()
            .Where(i => i.Id == invite.Id)
            .Select(i => new FcmInviteAcceptedProjection(
                i.Sender!.FcmToken,
                i.Sender.DeletedAt != null,
                i.Receiver!.DeletedAt != null,
                i.Receiver.FirstName,
                i.Place!.Name))
            .FirstOrDefaultAsync(ct);

        if (projection is null)
        {
            logger.LogDebug("FcmInviteNotifier: invite {InviteId} not found, skipping FCM accepted push", invite.Id);
            return;
        }

        if (projection.SenderDeleted)
        {
            logger.LogDebug("FcmInviteNotifier: invite {InviteId} sender soft-deleted, skipping FCM accepted push", invite.Id);
            return;
        }

        if (projection.ReceiverDeleted)
        {
            logger.LogDebug(
                "FcmInviteNotifier: invite {InviteId} receiver soft-deleted, skipping FCM (would leak Receiver.FirstName)",
                invite.Id);
            return;
        }

        if (string.IsNullOrEmpty(projection.FcmToken))
        {
            logger.LogDebug("FcmInviteNotifier: invite {InviteId} sender has no FCM token, skipping accepted push", invite.Id);
            return;
        }

        var (title, body) = PushTemplates.Accepted(projection.ReceiverFirstName, projection.PlaceName);
        await fcmClient.SendAsync(projection.FcmToken, title, body, ct);
    }

    /// <inheritdoc />
    public virtual Task InviteDeclinedAsync(Invite invite, CancellationToken ct)
    {
        logger.LogDebug("FcmInviteNotifier: silent decline — no FCM push for invite {InviteId}", invite.Id);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task InviteExpiredAsync(Invite invite, CancellationToken ct)
    {
        logger.LogDebug("FcmInviteNotifier: expiry FCM push deferred to future iteration for invite {InviteId}", invite.Id);
        return Task.CompletedTask;
    }
}
