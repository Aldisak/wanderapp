using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Features.Meetups.Shared;

/// <summary>Compact place projection used in meetup summary cards.</summary>
public record MeetupPlaceMiniDto(Guid Id, string Name, PlaceCategory Category);
