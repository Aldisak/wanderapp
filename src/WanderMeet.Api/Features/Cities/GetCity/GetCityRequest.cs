namespace WanderMeet.Api.Features.Cities.GetCity;

/// <summary>Route parameter for the get-city-by-id endpoint.</summary>
public record GetCityRequest
{
    /// <summary>The unique identifier of the city to retrieve.</summary>
    public Guid Id { get; init; }
}
