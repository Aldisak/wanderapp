using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Invites.Realtime.Shared;
using WanderMeet.Api.Features.Invites.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.Infrastructure.SignalR;

namespace WanderMeet.Api.Features.Invites.Realtime;

/// <summary>
/// SignalR-backed implementation of <see cref="IInviteNotifier"/>.
/// Pushes invite lifecycle events to connected clients via <see cref="InviteHub"/>.
/// </summary>
internal class SignalRInviteNotifier(
    IHubContext<InviteHub> hubContext,
    WanderMeetDbContext dbContext,
    ILogger<SignalRInviteNotifier> logger) : IInviteNotifier
{
    /// <inheritdoc />
    public virtual async Task InviteSentAsync(Invite invite, CancellationToken ct)
    {
        logger.LogDebug("SignalRInviteNotifier: pushing InviteReceived for invite {InviteId}", invite.Id);

        var data = await dbContext.Invites
            .AsNoTracking()
            .Where(i => i.Id == invite.Id)
            .Select(i => new
            {
                SenderId = i.Sender!.Id,
                SenderFirstName = i.Sender.FirstName,
                SenderDeleted = i.Sender.DeletedAt != null,
                SenderPhotoUrl = i.Sender.Photos.OrderBy(p => p.Order).Select(p => p.BlobUrl).FirstOrDefault(),
                TagSlug = i.HangoutTag!.Slug.ToString(),
                PlaceId = i.Place!.Id,
                PlaceName = i.Place.Name,
                PlaceCategory = i.Place.Category,
            })
            .FirstOrDefaultAsync(ct);

        if (data is null || data.SenderDeleted)
        {
            logger.LogDebug("SignalRInviteNotifier: invite {InviteId} sender missing or soft-deleted; skipping InviteReceived push", invite.Id);
            return;
        }

        var dto = new InviteHubReceivedDto(
            invite.Id,
            new InviteUserMiniDto(data.SenderId, data.SenderFirstName, data.SenderPhotoUrl),
            data.TagSlug,
            new InvitePlaceMiniDto(data.PlaceId, data.PlaceName, data.PlaceCategory),
            invite.SentAt,
            invite.ExpiresAt);

        await hubContext.Clients
            .User(invite.ReceiverId.ToString())
            .SendAsync("InviteReceived", dto, ct);
    }

    /// <inheritdoc />
    public virtual async Task InviteAcceptedAsync(Invite invite, Guid meetupId, CancellationToken ct)
    {
        logger.LogDebug("SignalRInviteNotifier: pushing InviteAccepted for invite {InviteId}", invite.Id);

        var dto = new InviteHubAcceptedDto(invite.Id, meetupId, invite.RespondedAt!.Value);

        await hubContext.Clients
            .User(invite.SenderId.ToString())
            .SendAsync("InviteAccepted", dto, ct);
    }

    /// <inheritdoc />
    public virtual async Task InviteDeclinedAsync(Invite invite, CancellationToken ct)
    {
        logger.LogDebug("SignalRInviteNotifier: pushing InviteDeclined for invite {InviteId}", invite.Id);

        var dto = new InviteHubDeclinedDto(invite.Id);

        await hubContext.Clients
            .User(invite.SenderId.ToString())
            .SendAsync("InviteDeclined", dto, ct);
    }

    /// <inheritdoc />
    public virtual async Task InviteExpiredAsync(Invite invite, CancellationToken ct)
    {
        logger.LogDebug("SignalRInviteNotifier: pushing InviteExpired for invite {InviteId}", invite.Id);

        var dto = new InviteHubExpiredDto(invite.Id);

        await hubContext.Clients
            .Users(invite.SenderId.ToString(), invite.ReceiverId.ToString())
            .SendAsync("InviteExpired", dto, ct);
    }
}
