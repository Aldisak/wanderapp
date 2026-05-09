namespace WanderMeet.Api.Features.Meetups.SubmitReview;

/// <summary>Projection of a persisted <c>MeetupReview</c> row.</summary>
public record ReviewDto(
    Guid Id,
    Guid MeetupId,
    Guid ReviewerId,
    Guid RevieweeId,
    bool DidMeet,
    bool FeltSafe,
    bool GoodConvo,
    bool WouldMeetAgain,
    string? Text,
    DateTimeOffset CreatedAt);
