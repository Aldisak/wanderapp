namespace WanderMeet.Api.Features.Meetups.Shared;

/// <summary>Summary of a meetup displayed in pending-review lists and meetup history.</summary>
public record MeetupSummaryDto(Guid MeetupId, MeetupUserMiniDto OtherUser, MeetupPlaceMiniDto Place, DateTimeOffset MetAt);
