using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Meetups.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Meetups.PendingReview;

/// <summary>Returns the list of meetups that the caller has not yet reviewed.</summary>
internal sealed class PendingReviewEndpoint(WanderMeetDbContext dbContext)
    : EndpointWithoutRequest<ListPendingReviewsResponse>
{
    private readonly MeetupsFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Get("meetups/pending-review");
        Description(b => b
            .WithName(nameof(PendingReviewEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "List meetups pending review";
            s.Description = "Returns up to 50 meetups (ordered by MetAt descending) that the caller has not yet reviewed.";
            s.Responses[StatusCodes.Status200OK] = "List of pending-review meetups";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "Caller has no user profile (User.NotRegistered)";
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

        var rows = await dbContext.Meetups.AsNoTracking()
            .Where(m => m.UserAId == callerId.Value || m.UserBId == callerId.Value)
            .Where(m => !dbContext.MeetupReviews.Any(r => r.MeetupId == m.Id && r.ReviewerId == callerId.Value))
            .OrderByDescending(m => m.MetAt)
            .Take(50)
            .Select(m => new MeetupSummaryDto(
                m.Id,
                m.UserAId == callerId.Value
                    ? new MeetupUserMiniDto(
                        m.UserB!.Id,
                        m.UserB.FirstName,
                        m.UserB.Photos.Where(p => p.DeletedAt == null).OrderBy(p => p.Order).Select(p => p.BlobUrl).FirstOrDefault())
                    : new MeetupUserMiniDto(
                        m.UserA!.Id,
                        m.UserA.FirstName,
                        m.UserA.Photos.Where(p => p.DeletedAt == null).OrderBy(p => p.Order).Select(p => p.BlobUrl).FirstOrDefault()),
                new MeetupPlaceMiniDto(m.Place!.Id, m.Place.Name, m.Place.Category),
                m.MetAt))
            .ToListAsync(ct);

        await Send.OkAsync(new ListPendingReviewsResponse(rows), ct);
    }
}
