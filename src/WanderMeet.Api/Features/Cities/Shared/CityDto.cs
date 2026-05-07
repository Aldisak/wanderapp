namespace WanderMeet.Api.Features.Cities.Shared;

/// <summary>Public representation of a city reference record.</summary>
/// <param name="Id">Unique identifier of the city.</param>
/// <param name="Name">Display name (English).</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code.</param>
/// <param name="Latitude">Geographic latitude (degrees north).</param>
/// <param name="Longitude">Geographic longitude (degrees east).</param>
/// <param name="CreatedAt">Timestamp when the city was added to the reference data.</param>
public record CityDto(Guid Id, string Name, string Country, double Latitude, double Longitude, DateTimeOffset CreatedAt);
