using WanderMeet.Api.Features.Meetups.Shared;

namespace WanderMeet.Api.Features.Meetups.SubmitReview;

/// <summary>Response for POST /api/v1/meetups/{id}/review.</summary>
public record SubmitReviewResponse(ReviewDto Review, RevieweeStatsDto Reviewee);
