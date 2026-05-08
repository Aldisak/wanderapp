namespace WanderMeet.Api.Features.Places.SuggestPlaces;

/// <summary>Query parameters for the suggest-places endpoint.</summary>
public record SuggestPlacesRequest
{
    /// <summary>City to suggest places within.</summary>
    public Guid CityId { get; init; }

    /// <summary>Optional hangout tag slug to filter by matching place category.</summary>
    public string? HangoutTagSlug { get; init; }

    /// <summary>Caller's latitude (-90 to 90).</summary>
    public double Lat { get; init; }

    /// <summary>Caller's longitude (-180 to 180).</summary>
    public double Lng { get; init; }
}
