namespace WanderMeet.Api.Features.Meetups.Shared;

/// <summary>Compact user projection used in meetup summary cards.</summary>
public record MeetupUserMiniDto(Guid Id, string FirstName, string? PhotoUrl);
