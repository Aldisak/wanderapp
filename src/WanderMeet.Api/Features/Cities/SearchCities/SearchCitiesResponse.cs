using WanderMeet.Api.Features.Cities.Shared;

namespace WanderMeet.Api.Features.Cities.SearchCities;

/// <summary>Response payload for the city search endpoint.</summary>
/// <param name="Items">Matching cities, ordered alphabetically.</param>
public record SearchCitiesResponse(IReadOnlyList<CityDto> Items);
