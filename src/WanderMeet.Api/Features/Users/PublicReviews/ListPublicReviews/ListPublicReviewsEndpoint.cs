using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Users.PublicReviews.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;

namespace WanderMeet.Api.Features.Users.PublicReviews.ListPublicReviews;

/// <summary>Returns the paginated list of public (DidMeet=true) reviews for a target user.</summary>
internal sealed class ListPublicReviewsEndpoint(WanderMeetDbContext dbContext)
    : Endpoint<ListPublicReviewsRequest, ListPublicReviewsResponse>
{
    private readonly UsersFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Get("users/{id:guid}/reviews");
        Description(b => b
            .WithName(nameof(ListPublicReviewsEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "List public reviews for a user";
            s.Description = "Returns up to 50 DidMeet=true reviews for the target user, ordered by CreatedAt descending.";
            s.Responses[StatusCodes.Status200OK] = "List of public reviews";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "Target user not found or soft-deleted";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(ListPublicReviewsRequest req, CancellationToken ct)
    {
        var targetExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == req.Id && u.DeletedAt == null, ct);

        if (!targetExists)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var items = await dbContext.MeetupReviews
            .AsNoTracking()
            .Where(r => r.RevieweeId == req.Id && r.DidMeet)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new PublicReviewDto(
                r.Id,
                new ReviewerMiniDto(
                    r.Reviewer!.Id,
                    r.Reviewer.FirstName,
                    r.Reviewer.Photos
                        .Where(p => p.DeletedAt == null)
                        .OrderBy(p => p.Order)
                        .Select(p => p.BlobUrl)
                        .FirstOrDefault()),
                r.FeltSafe,
                r.GoodConvo,
                r.WouldMeetAgain,
                r.Text,
                r.CreatedAt))
            .ToListAsync(ct);

        await Send.OkAsync(new ListPublicReviewsResponse(items), ct);
    }
}
