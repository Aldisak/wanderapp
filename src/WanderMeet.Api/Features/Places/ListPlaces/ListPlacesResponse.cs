using WanderMeet.Api.Features.Places.Shared;

namespace WanderMeet.Api.Features.Places.ListPlaces;

/// <summary>Response payload for the list-places endpoint.</summary>
public record ListPlacesResponse(IReadOnlyList<PlaceDto> Items);
