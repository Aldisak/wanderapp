using NetTopologySuite.Geometries;
using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Database.Entities;

/// <summary>A real-world venue surfaced by Google Places and ranked by Wander meetups.</summary>
public class Place : AuditableEntity
{
    /// <summary>Stable Google Places identifier; unique.</summary>
    public required string GooglePlaceId { get; set; }

    /// <summary>Display name from Google Places.</summary>
    public required string Name { get; set; }

    /// <summary>City this place belongs to.</summary>
    public required Guid CityId { get; set; }

    /// <inheritdoc cref="CityId" />
    public City? City { get; set; }

    /// <summary>Geographic coordinate; SRID 4326.</summary>
    public required Point Location { get; set; }

    /// <summary>One of five categories used to filter the Places tab.</summary>
    public required PlaceCategory Category { get; set; }

    /// <summary>Wifi signalled by Google Places.</summary>
    public bool HasWifi { get; set; }

    /// <summary>Quiet ambience signalled by Google Places.</summary>
    public bool IsQuiet { get; set; }

    /// <summary>Solo-friendly heuristic from Google Places.</summary>
    public bool IsSoloFriendly { get; set; }

    /// <summary>Google's 1-decimal star rating (0.0 – 5.0).</summary>
    public decimal? GoogleRating { get; set; }

    /// <summary>Number of confirmed Wander meetups at this place — incremented on did-meet review.</summary>
    public int WanderMeetupCount { get; set; }

    /// <summary>True when an advertiser has paid to occupy slot 3 in suggestions.</summary>
    public bool IsSponsored { get; set; }

    /// <summary>Free-text perk shown alongside sponsored placement (e.g. "Free day pass").</summary>
    public string? SponsorPerk { get; set; }
}
