using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Users.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared;
using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Features.Discovery.Feed;

/// <summary>Returns a cursor-paginated list of nearby users open to meetups today.</summary>
internal sealed class DiscoverFeedEndpoint(WanderMeetDbContext dbContext, TimeProvider timeProvider)
    : Endpoint<DiscoverFeedRequest, DiscoverFeedResponse>
{
    private readonly DiscoveryFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Get("discover");
        Description(b => b
            .WithName(nameof(DiscoverFeedEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.Discovery));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Discover nearby users";
            s.Description = "Returns a cursor-paginated list of nearby users who are open to meetups today.";
            s.Responses[StatusCodes.Status200OK] = "List of public user profiles with optional next cursor";
            s.Responses[StatusCodes.Status400BadRequest] = "Validation error";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "Caller has no user profile or city not found";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(DiscoverFeedRequest req, CancellationToken ct)
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

        var city = await dbContext.Cities
            .AsNoTracking()
            .Where(c => c.Id == req.CityId && c.DeletedAt == null)
            .Select(c => new { c.Id, c.Location })
            .FirstOrDefaultAsync(ct);

        if (city is null)
        {
            AddError(ErrorCodes.Discovery.CityNotFound, "City not found.");
            await Send.ErrorsAsync(404, ct);
            return;
        }

        var cityLocation = city.Location;

        var activeSince = timeProvider.GetUtcNow().Subtract(ValidationConstants.DiscoveryActiveWindow);

        // Resolve optional hangout tag id before the main query to avoid N+1
        Guid? hangoutTagId = null;
        if (req.HangoutTagSlug is not null
            && Enum.TryParse<HangoutTagSlug>(req.HangoutTagSlug, ignoreCase: true, out var slugEnum))
        {
            hangoutTagId = await dbContext.HangoutTags
                .AsNoTracking()
                .Where(h => h.Slug == slugEnum)
                .Select(h => (Guid?)h.Id)
                .FirstOrDefaultAsync(ct);
        }

        // Decode cursor (already validated by validator)
        DiscoveryCursor? cursor = null;
        if (req.Cursor is not null && DiscoveryCursor.TryDecode(req.Cursor, out var decodedCursor))
            cursor = decodedCursor;

        var query = dbContext.Users.AsNoTracking()
            .Where(u => u.Id != callerId.Value)
            .Where(u => u.DeletedAt == null)
            .Where(u => u.IsOpenToday)
            .Where(u => u.LastActiveAt > activeSince)
            .Where(u => u.Location != null
                        && EF.Functions.IsWithinDistance(u.Location!, cityLocation, ValidationConstants.DiscoveryRadiusMetres, useSpheroid: true));

        if (hangoutTagId is { } tagId)
            query = query.Where(u => u.HangoutTags.Any(uht => uht.HangoutTagId == tagId));

        query = query.Where(u => !dbContext.Invites.Any(i =>
            i.Status == InviteStatus.Pending &&
            ((i.SenderId == callerId.Value && i.ReceiverId == u.Id) ||
             (i.SenderId == u.Id && i.ReceiverId == callerId.Value))));

        if (cursor is { } c)
        {
            query = query.Where(u =>
                (!u.IsOpenToday && c.IsOpenToday) ||
                (u.IsOpenToday == c.IsOpenToday && u.TrustScore < c.TrustScore) ||
                (u.IsOpenToday == c.IsOpenToday && u.TrustScore == c.TrustScore && u.LastActiveAt < c.LastActiveAt) ||
                (u.IsOpenToday == c.IsOpenToday && u.TrustScore == c.TrustScore && u.LastActiveAt == c.LastActiveAt && u.Id < c.Id));
        }

        var rows = await query
            .OrderByDescending(u => u.IsOpenToday)
            .ThenByDescending(u => u.TrustScore)
            .ThenByDescending(u => u.LastActiveAt)
            .ThenByDescending(u => u.Id)
            .Take(req.Limit + 1)
            .Select(u => new PublicUserDto(
                u.Id,
                u.FirstName,
                u.Bio,
                u.IsIdVerified,
                u.IsOpenToday,
                u.IsOpenToRomance,
                u.LastActiveAt,
                u.TrustScore,
                u.MeetupCount,
                u.CitiesCount,
                u.YearsNomading,
                u.CityId,
                u.CreatedAt,
                u.HangoutTags.Select(ht => ht.HangoutTagId).ToList()))
            .ToListAsync(ct);

        string? nextCursor = null;
        if (rows.Count == req.Limit + 1)
        {
            rows.RemoveAt(rows.Count - 1);
            var last = rows[^1];
            nextCursor = DiscoveryCursor.Encode(new DiscoveryCursor(
                last.LastActiveAt,
                last.TrustScore,
                last.Id,
                last.IsOpenToday));
        }

        await Send.OkAsync(new DiscoverFeedResponse(rows, nextCursor), ct);
    }
}
