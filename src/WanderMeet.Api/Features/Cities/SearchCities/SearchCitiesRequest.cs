namespace WanderMeet.Api.Features.Cities.SearchCities;

/// <summary>Query parameters for the city search endpoint.</summary>
public record SearchCitiesRequest
{
    /// <summary>Search term; matched against city name (case-insensitive).</summary>
    public required string Q { get; init; }

    /// <summary>Maximum number of results to return (1–50, default 20).</summary>
    public int Limit { get; init; } = 20;
}
