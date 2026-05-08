using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Features.Places.Shared;

/// <summary>Represents a place with all fields for client display.</summary>
public record PlaceDto(
    Guid Id,
    string Name,
    Guid CityId,
    double Latitude,
    double Longitude,
    PlaceCategory Category,
    bool HasWifi,
    bool IsQuiet,
    bool IsSoloFriendly,
    decimal? GoogleRating,
    int WanderMeetupCount,
    bool IsSponsored,
    string? SponsorPerk,
    DateTimeOffset CreatedAt);
