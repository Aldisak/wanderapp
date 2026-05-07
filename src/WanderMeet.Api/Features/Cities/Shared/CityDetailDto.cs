namespace WanderMeet.Api.Features.Cities.Shared;

/// <summary>Detailed city representation including the count of currently active nomads.</summary>
/// <param name="City">Core city data.</param>
/// <param name="ActiveNomadCount">Number of registered users currently in this city who are open today and recently active.</param>
public record CityDetailDto(CityDto City, int ActiveNomadCount);
