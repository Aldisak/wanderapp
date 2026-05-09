using WanderMeet.Api.Features.Users.PublicReviews.Shared;

namespace WanderMeet.Api.Features.Users.PublicReviews.ListPublicReviews;

/// <summary>Response payload for the list-public-reviews endpoint.</summary>
public record ListPublicReviewsResponse(IReadOnlyList<PublicReviewDto> Items);
