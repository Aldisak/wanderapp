using NetTopologySuite.Geometries;

namespace WanderMeet.Api.Database.Entities;

/// <summary>Reference data row representing a single city.</summary>
public class City : AuditableEntity
{
    /// <summary>Display name (English).</summary>
    public required string Name { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public required string Country { get; set; }

    /// <summary>City-center coordinate; SRID 4326.</summary>
    public required Point Location { get; set; }
}
