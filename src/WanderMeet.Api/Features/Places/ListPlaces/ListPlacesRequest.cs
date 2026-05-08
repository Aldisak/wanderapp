namespace WanderMeet.Api.Features.Places.ListPlaces;

/// <summary>Query parameters for the list-places endpoint.</summary>
public record ListPlacesRequest
{
    /// <summary>City to list places for.</summary>
    public Guid CityId { get; init; }

    /// <summary>Optional category filter; must be a valid <see cref="WanderMeet.Shared.Enums.PlaceCategory"/> string if provided.</summary>
    public string? Category { get; init; }
}
