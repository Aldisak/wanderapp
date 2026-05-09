using WanderMeet.Api.Features.Meetups.Shared;

namespace WanderMeet.Api.Features.Meetups.PendingReview;

/// <summary>Response for GET /api/v1/meetups/pending-review.</summary>
public record ListPendingReviewsResponse(IReadOnlyList<MeetupSummaryDto> Items);
