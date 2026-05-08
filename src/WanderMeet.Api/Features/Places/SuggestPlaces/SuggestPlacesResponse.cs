using WanderMeet.Api.Features.Places.Shared;

namespace WanderMeet.Api.Features.Places.SuggestPlaces;

/// <summary>Response payload for the suggest-places endpoint.</summary>
public record SuggestPlacesResponse(IReadOnlyList<PlaceDto> Items);
