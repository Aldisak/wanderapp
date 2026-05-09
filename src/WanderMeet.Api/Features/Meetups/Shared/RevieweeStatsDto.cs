namespace WanderMeet.Api.Features.Meetups.Shared;

/// <summary>Reviewee stats returned in the submit-review response and reusable across meetup-related endpoints.</summary>
public record RevieweeStatsDto(Guid Id, int TrustScore, int MeetupCount);
