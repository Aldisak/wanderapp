using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Invites.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared;
using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Features.Invites.DeclineInvite;

/// <summary>Declines a pending invite addressed to the authenticated user. A notifier event fires for the sender side but the receiver still sees no UI toast (silent on the UI).</summary>
internal sealed class DeclineInviteEndpoint(
    WanderMeetDbContext dbContext,
    IInviteNotifier notifier,
    TimeProvider timeProvider,
    ILogger<DeclineInviteEndpoint> logger)
    : Endpoint<DeclineInviteRequest, InviteDto>
{
    private readonly InvitesFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Patch("invites/{id:guid}/decline");
        Description(b => b
            .WithName(nameof(DeclineInviteEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Decline an invite";
            s.Description = "Declines the specified pending invite if the caller is the receiver. A notifier event fires for the sender side but the receiver still sees no UI toast (silent on the UI).";
            s.Responses[StatusCodes.Status200OK] = "Invite declined; returns the updated InviteDto";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "Invite not found or caller is not the receiver";
            s.Responses[StatusCodes.Status409Conflict] = "Invite is already resolved";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(DeclineInviteRequest req, CancellationToken ct)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        // Load tracked caller (to update LastActiveAt)
        var caller = await dbContext.Users
            .FirstOrDefaultAsync(u => u.AzureAdB2CId == sub && u.DeletedAt == null, ct);

        if (caller is null)
        {
            AddError(ErrorCodes.User.NotRegistered, "No user profile found for this identity.");
            await Send.ErrorsAsync(404, ct);
            return;
        }

        // Load tracked invite scoped to receiver = caller (covers both "not found" and "foreign invite" → 404)
        var invite = await dbContext.Invites
            .FirstOrDefaultAsync(i => i.Id == req.Id && i.ReceiverId == caller.Id, ct);

        if (invite is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Guard: invite must be Pending
        if (invite.Status != InviteStatus.Pending)
        {
            AddError(ErrorCodes.Invite.AlreadyResolved, "This invite has already been resolved.");
            await Send.ErrorsAsync(409, ct);
            return;
        }

        var now = timeProvider.GetUtcNow();

        // Mutate invite
        invite.Status = InviteStatus.Declined;
        invite.RespondedAt = now;
        invite.UpdatedAt = now;

        caller.LastActiveAt = now;

        await dbContext.SaveChangesAsync(ct);

        // Notify — failures MUST NOT bubble
        try
        {
            await notifier.InviteDeclinedAsync(invite, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "IInviteNotifier.InviteDeclinedAsync failed for invite {InviteId}; continuing.", invite.Id);
        }

        // Project fresh DTO via AsNoTracking query (mirrors AcceptInviteEndpoint).
        var dto = await dbContext.Invites
            .AsNoTracking()
            .Where(i => i.Id == invite.Id)
            .Select(i => new InviteDto(
                i.Id,
                new InviteUserMiniDto(
                    i.Sender!.Id,
                    i.Sender.FirstName,
                    i.Sender.Photos.Where(p => p.DeletedAt == null).OrderBy(p => p.Order).Select(p => p.BlobUrl).FirstOrDefault()),
                new InviteUserMiniDto(
                    i.Receiver!.Id,
                    i.Receiver.FirstName,
                    i.Receiver.Photos.Where(p => p.DeletedAt == null).OrderBy(p => p.Order).Select(p => p.BlobUrl).FirstOrDefault()),
                i.HangoutTagId,
                i.HangoutTag!.Slug.ToString(),
                new InvitePlaceMiniDto(i.Place!.Id, i.Place.Name, i.Place.Category),
                i.SenderIsThere,
                i.Status,
                i.SentAt,
                i.RespondedAt,
                i.ExpiresAt))
            .FirstAsync(ct);

        await Send.OkAsync(dto, ct);
    }
}
