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

namespace WanderMeet.Api.Features.Invites.SendInvite;

/// <summary>Sends an invite from the authenticated user to another user.</summary>
internal sealed class SendInviteEndpoint(
    WanderMeetDbContext dbContext,
    IInviteNotifier inviteNotifier,
    TimeProvider timeProvider,
    ILogger<SendInviteEndpoint> logger)
    : Endpoint<SendInviteRequest, InviteDto>
{
    private readonly InvitesFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Post("invites");
        Description(b => b
            .WithName(nameof(SendInviteEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.InviteSend));
        DontCatchExceptions();
        DontThrowIfValidationFails();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Send an invite to another user";
            s.Description = "Creates a new Pending invite from the caller to the specified receiver. Enforces same-city and no-duplicate-pending rules.";
            s.Responses[StatusCodes.Status201Created] = "Invite created";
            s.Responses[StatusCodes.Status400BadRequest] = "Validation error or business-rule violation (self-invite, unknown receiver/tag/place, city mismatch)";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "Caller has no user profile (User.NotRegistered)";
            s.Responses[StatusCodes.Status409Conflict] = "A Pending invite already exists between caller and receiver in either direction";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded (20 invites/hour per user)";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(SendInviteRequest req, CancellationToken ct)
    {
        if (ValidationFailed)
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

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

        // Guard: self-invite (cheap, no DB lookup needed)
        if (req.ReceiverId == caller.Id)
        {
            AddError(ErrorCodes.Invite.SelfInviteForbidden, "You cannot invite yourself.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        // Load receiver (AsNoTracking projection)
        var receiver = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == req.ReceiverId && u.DeletedAt == null)
            .Select(u => new { u.Id, u.CityId, u.FirstName, PhotoUrl = u.Photos.OrderBy(p => p.Order).Select(p => p.BlobUrl).FirstOrDefault() })
            .FirstOrDefaultAsync(ct);

        if (receiver is null)
        {
            AddError(ErrorCodes.Invite.ReceiverNotFound, "The specified receiver was not found.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        // Check hangout tag exists
        var tagExists = await dbContext.HangoutTags
            .AsNoTracking()
            .AnyAsync(h => h.Id == req.HangoutTagId, ct);

        if (!tagExists)
        {
            AddError(ErrorCodes.Invite.HangoutTagNotFound, "The specified hangout tag was not found.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        // Load place (AsNoTracking projection)
        var place = await dbContext.Places
            .AsNoTracking()
            .Where(p => p.Id == req.PlaceId && p.DeletedAt == null)
            .Select(p => new { p.Id, p.CityId, p.Name, p.Category })
            .FirstOrDefaultAsync(ct);

        if (place is null)
        {
            AddError(ErrorCodes.Invite.PlaceNotFound, "The specified place was not found.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        // Guard: place city must match receiver city
        if (place.CityId != receiver.CityId)
        {
            AddError(ErrorCodes.Invite.PlaceCityMismatch, "The place is not in the receiver's current city.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        // Guard: no pending invite in either direction
        var pendingExists = await dbContext.Invites
            .AsNoTracking()
            .AnyAsync(i =>
                i.Status == InviteStatus.Pending &&
                ((i.SenderId == caller.Id && i.ReceiverId == receiver.Id) ||
                 (i.SenderId == receiver.Id && i.ReceiverId == caller.Id)), ct);

        if (pendingExists)
        {
            AddError(ErrorCodes.Invite.AlreadyPending, "A pending invite already exists between you and this user.");
            await Send.ErrorsAsync(409, ct);
            return;
        }

        // Load hangout tag slug for DTO
        var hangoutTag = await dbContext.HangoutTags
            .AsNoTracking()
            .Where(h => h.Id == req.HangoutTagId)
            .Select(h => new { h.Id, h.Slug })
            .FirstAsync(ct);

        var now = timeProvider.GetUtcNow();
        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            SenderId = caller.Id,
            ReceiverId = receiver.Id,
            HangoutTagId = req.HangoutTagId,
            PlaceId = req.PlaceId,
            SenderIsThere = req.SenderIsThere,
            Status = InviteStatus.Pending,
            SentAt = now,
            ExpiresAt = now + ValidationConstants.InviteExpiryWindow,
            RespondedAt = null,
            CreatedAt = now,
        };

        dbContext.Invites.Add(invite);
        caller.LastActiveAt = now;
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Partial unique index ix_invites_sender_receiver_pending_unique fired —
            // a concurrent request created a Pending invite for the same (sender, receiver)
            // pair after our AnyAsync check (TOCTOU race; security audit finding F5).
            AddError(ErrorCodes.Invite.AlreadyPending, "A pending invite already exists between you and this user.");
            await Send.ErrorsAsync(409, ct);
            return;
        }

        // Notify — failures MUST NOT bubble
        try
        {
            await inviteNotifier.InviteSentAsync(invite, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "IInviteNotifier.InviteSentAsync failed for invite {InviteId}; continuing.", invite.Id);
        }

        var callerPhotoUrl = await dbContext.UserPhotos
            .AsNoTracking()
            .Where(p => p.UserId == caller.Id)
            .OrderBy(p => p.Order)
            .Select(p => p.BlobUrl)
            .FirstOrDefaultAsync(ct);

        var dto = new InviteDto(
            invite.Id,
            new InviteUserMiniDto(caller.Id, caller.FirstName, callerPhotoUrl),
            new InviteUserMiniDto(receiver.Id, receiver.FirstName, receiver.PhotoUrl),
            hangoutTag.Id,
            hangoutTag.Slug.ToString(),
            new InvitePlaceMiniDto(place.Id, place.Name, place.Category),
            invite.SenderIsThere,
            invite.Status,
            invite.SentAt,
            invite.RespondedAt,
            invite.ExpiresAt);

        await Send.ResponseAsync(dto, StatusCodes.Status201Created, ct);
    }

    /// <summary>
    /// Returns true if the supplied EF Core <see cref="DbUpdateException"/> wraps a PostgreSQL
    /// unique-violation error (SQLSTATE 23505). Used to convert a partial-unique-index race
    /// into a clean 409 response.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
}
