using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Invites.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Invites.ListSent;

/// <summary>Returns up to 50 sent invites (all statuses) where the caller is the sender, ordered by SentAt descending.</summary>
internal sealed class ListSentInvitesEndpoint(WanderMeetDbContext dbContext)
    : EndpointWithoutRequest<ListInvitesResponse>
{
    private readonly InvitesFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Get("invites/sent");
        Description(b => b
            .WithName(nameof(ListSentInvitesEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "List sent invites";
            s.Description = "Returns up to 50 invites sent by the caller (all statuses, non-deleted receivers). Capped at 50 — no cursor pagination.";
            s.Responses[StatusCodes.Status200OK] = "Sent invites, all statuses, ordered SentAt DESC";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "Caller has no user profile";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(CancellationToken ct)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var callerId = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.AzureAdB2CId == sub && u.DeletedAt == null)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);

        if (callerId is null)
        {
            AddError(ErrorCodes.User.NotRegistered, "No user profile found for this identity.");
            await Send.ErrorsAsync(404, ct);
            return;
        }

        var items = await dbContext.Invites
            .AsNoTracking()
            .Where(i => i.SenderId == callerId.Value && i.Receiver!.DeletedAt == null)
            .OrderByDescending(i => i.SentAt)
            .Take(50)
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
            .ToListAsync(ct);

        await Send.OkAsync(new ListInvitesResponse(items), ct);
    }
}
