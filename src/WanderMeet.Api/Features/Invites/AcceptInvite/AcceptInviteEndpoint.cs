using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Invites.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared;
using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Features.Invites.AcceptInvite;

/// <summary>Accepts a pending invite addressed to the authenticated user.</summary>
internal sealed class AcceptInviteEndpoint(
    WanderMeetDbContext dbContext,
    IInviteNotifier notifier,
    TimeProvider timeProvider,
    ILogger<AcceptInviteEndpoint> logger)
    : Endpoint<AcceptInviteRequest, AcceptInviteResponse>
{
    private readonly InvitesFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Patch("invites/{id:guid}/accept");
        Description(b => b
            .WithName(nameof(AcceptInviteEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Accept an invite";
            s.Description = "Accepts the specified pending invite if the caller is the receiver. Creates a Meetup row atomically.";
            s.Responses[StatusCodes.Status200OK] = "Invite accepted; returns updated InviteDto and the new MeetupId";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "Invite not found or caller is not the receiver";
            s.Responses[StatusCodes.Status409Conflict] = "Invite is already resolved or expired";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(AcceptInviteRequest req, CancellationToken ct)
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

        var now = timeProvider.GetUtcNow();

        // Guard: invite must be Pending and not expired
        if (invite.Status != InviteStatus.Pending || invite.ExpiresAt <= now)
        {
            AddError(ErrorCodes.Invite.AlreadyResolved, "This invite has already been resolved or has expired.");
            await Send.ErrorsAsync(409, ct);
            return;
        }

        // Mutate invite
        invite.Status = InviteStatus.Accepted;
        invite.RespondedAt = now;
        invite.UpdatedAt = now;

        // Create meetup
        var meetup = new Meetup
        {
            Id = Guid.NewGuid(),
            InviteId = invite.Id,
            UserAId = invite.SenderId,
            UserBId = invite.ReceiverId,
            PlaceId = invite.PlaceId,
            MetAt = now,
            PromptSent = false,
            CreatedAt = now,
        };
        dbContext.Meetups.Add(meetup);

        caller.LastActiveAt = now;

        await dbContext.SaveChangesAsync(ct);

        // Notify — failures MUST NOT bubble
        try
        {
            await notifier.InviteAcceptedAsync(invite, meetup.Id, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notifier failed {InviteId} {Phase}", invite.Id, "InviteAccepted");
        }

        // Project fresh DTO via AsNoTracking query
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

        await Send.OkAsync(new AcceptInviteResponse(dto, meetup.Id), ct);
    }
}
