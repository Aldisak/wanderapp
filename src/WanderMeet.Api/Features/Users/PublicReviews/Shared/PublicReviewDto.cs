namespace WanderMeet.Api.Features.Users.PublicReviews.Shared;

/// <summary>A single public review item returned by the list-public-reviews endpoint.</summary>
public record PublicReviewDto(
    Guid Id,
    ReviewerMiniDto Reviewer,
    bool FeltSafe,
    bool GoodConvo,
    bool WouldMeetAgain,
    string? Text,
    DateTimeOffset CreatedAt);
