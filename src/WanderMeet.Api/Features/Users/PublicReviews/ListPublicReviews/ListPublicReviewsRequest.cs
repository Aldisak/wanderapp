namespace WanderMeet.Api.Features.Users.PublicReviews.ListPublicReviews;

/// <summary>Route-bound request for listing a user's public reviews.</summary>
public record ListPublicReviewsRequest
{
    /// <summary>The target user whose reviews are requested.</summary>
    public Guid Id { get; init; }
}
