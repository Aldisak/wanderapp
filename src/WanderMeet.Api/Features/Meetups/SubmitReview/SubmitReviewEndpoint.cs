using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Meetups.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Meetups.SubmitReview;

/// <summary>Submits a post-meetup review from the authenticated user.</summary>
internal sealed class SubmitReviewEndpoint(WanderMeetDbContext dbContext, TimeProvider timeProvider)
    : Endpoint<SubmitReviewRequest, SubmitReviewResponse>
{
    private readonly MeetupsFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Post("meetups/{id:guid}/review");
        Description(b => b
            .WithName(nameof(SubmitReviewEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        DontThrowIfValidationFails();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Submit a post-meetup review";
            s.Description = "Records the caller's review for a confirmed meetup and recomputes the reviewee's trust score.";
            s.Responses[StatusCodes.Status200OK] = "Review submitted; returns review and updated reviewee stats";
            s.Responses[StatusCodes.Status400BadRequest] = "Validation failure (e.g. text too long)";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "Meetup not found or caller is not a participant";
            s.Responses[StatusCodes.Status409Conflict] = "Caller has already reviewed this meetup";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(SubmitReviewRequest req, CancellationToken ct)
    {
        if (ValidationFailed)
        {
            var failures = ValidationFailures.ToList();
            ValidationFailures.Clear();
            foreach (var f in failures)
                AddError(f.ErrorCode ?? f.PropertyName, f.ErrorMessage);
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

        // Load tracked meetup; covers both "unknown id" and "foreign meetup" → 404 (never 403)
        var meetup = await dbContext.Meetups
            .FirstOrDefaultAsync(m => m.Id == req.Id && (m.UserAId == caller.Id || m.UserBId == caller.Id), ct);

        if (meetup is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var revieweeId = meetup.UserAId == caller.Id ? meetup.UserBId : meetup.UserAId;

        // Guard: caller already reviewed this meetup
        var alreadyReviewed = await dbContext.MeetupReviews
            .AsNoTracking()
            .AnyAsync(r => r.MeetupId == meetup.Id && r.ReviewerId == caller.Id, ct);

        if (alreadyReviewed)
        {
            AddError(ErrorCodes.Meetup.AlreadyReviewed, "You have already submitted a review for this meetup.");
            await Send.ErrorsAsync(409, ct);
            return;
        }

        var now = timeProvider.GetUtcNow();

        // Create new review
        var review = new MeetupReview
        {
            Id = Guid.NewGuid(),
            MeetupId = meetup.Id,
            ReviewerId = caller.Id,
            RevieweeId = revieweeId,
            DidMeet = req.DidMeet,
            FeltSafe = req.FeltSafe,
            GoodConvo = req.GoodConvo,
            WouldMeetAgain = req.WouldMeetAgain,
            Text = req.Text,
            CreatedAt = now,
        };
        dbContext.MeetupReviews.Add(review);

        // Increment place meetup count if meetup actually happened
        if (req.DidMeet)
        {
            var place = await dbContext.Places.FirstAsync(p => p.Id == meetup.PlaceId, ct);
            place.WanderMeetupCount += 1;
        }

        caller.LastActiveAt = now;

        // First save: persist review + place increment + caller LastActiveAt
        await dbContext.SaveChangesAsync(ct);

        // Recompute reviewee stats (new review is already saved — included in aggregation)
        var stats = await dbContext.MeetupReviews
            .AsNoTracking()
            .Where(r => r.RevieweeId == revieweeId)
            .GroupBy(r => 1)
            .Select(g => new
            {
                MeetupCount = g.Count(r => r.DidMeet),
                FeltSafeCount = g.Count(r => r.FeltSafe),
                WouldMeetAgainCount = g.Count(r => r.WouldMeetAgain),
                GoodConvoCount = g.Count(r => r.GoodConvo),
            })
            .FirstOrDefaultAsync(ct) ?? new { MeetupCount = 0, FeltSafeCount = 0, WouldMeetAgainCount = 0, GoodConvoCount = 0 };

        var (trustScore, meetupCount) = TrustScoreCalculator.Compute(
            stats.MeetupCount,
            stats.FeltSafeCount,
            stats.WouldMeetAgainCount,
            stats.GoodConvoCount);

        // Load tracked reviewee; soft-deleted reviewees still receive updated stats per spec
        var reviewee = await dbContext.Users.FirstAsync(u => u.Id == revieweeId, ct);
        reviewee.TrustScore = trustScore;
        reviewee.MeetupCount = meetupCount;

        // Second save: persist reviewee TrustScore + MeetupCount
        await dbContext.SaveChangesAsync(ct);

        var reviewDto = new ReviewDto(
            review.Id,
            review.MeetupId,
            review.ReviewerId,
            review.RevieweeId,
            review.DidMeet,
            review.FeltSafe,
            review.GoodConvo,
            review.WouldMeetAgain,
            review.Text,
            review.CreatedAt);

        var statsDto = new RevieweeStatsDto(reviewee.Id, reviewee.TrustScore, reviewee.MeetupCount);

        await Send.OkAsync(new SubmitReviewResponse(reviewDto, statsDto), ct);
    }
}
