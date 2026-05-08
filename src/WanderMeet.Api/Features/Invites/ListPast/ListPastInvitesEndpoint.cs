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

namespace WanderMeet.Api.Features.Invites.ListPast;

/// <summary>Returns up to 50 past (non-Pending) invites where the caller is sender or receiver, ordered by RespondedAt DESC NULLS LAST then SentAt DESC.</summary>
internal sealed class ListPastInvitesEndpoint(WanderMeetDbContext dbContext)
    : EndpointWithoutRequest<ListInvitesResponse>
{
    private readonly InvitesFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Get("invites/past");
        Description(b => b
            .WithName(nameof(ListPastInvitesEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "List past invites";
            s.Description = "Returns up to 50 non-Pending invites where the caller is the sender or receiver. Capped at 50 — no cursor pagination.";
            s.Responses[StatusCodes.Status200OK] = "Past invites, ordered RespondedAt DESC NULLS LAST then SentAt DESC";
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

        // NULLS LAST: OrderBy(i => i.RespondedAt == null) pushes nulls to end (false < true in DB boolean sort)
        var items = await dbContext.Invites
            .AsNoTracking()
            .Where(i => (i.SenderId == callerId.Value || i.ReceiverId == callerId.Value)
                        && i.Status != InviteStatus.Pending
                        && i.Sender!.DeletedAt == null
                        && i.Receiver!.DeletedAt == null)
            .OrderBy(i => i.RespondedAt == null)
            .ThenByDescending(i => i.RespondedAt)
            .ThenByDescending(i => i.SentAt)
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
