using NetTopologySuite.Geometries;

namespace WanderMeet.Api.Database.Entities;

/// <summary>End-user profile. Soft-deleted via <see cref="AuditableEntity.DeletedAt"/>.</summary>
public class User : AuditableEntity
{
    /// <summary>Stable identifier issued by Azure AD B2C; used to map JWT subject to the local user.</summary>
    public required string AzureAdB2CId { get; set; }

    /// <summary>Display name shown across the app.</summary>
    public required string FirstName { get; set; }

    /// <summary>Optional self-introduction; max 160 chars.</summary>
    public string? Bio { get; set; }

    /// <summary>True after Stripe Identity ID-verification passes.</summary>
    public bool IsIdVerified { get; set; }

    /// <summary>True while the user is open to meetups today; reset by a daily background job.</summary>
    public bool IsOpenToday { get; set; }

    /// <summary>True if the user opts into romantic matching as well as platonic meetups.</summary>
    public bool IsOpenToRomance { get; set; }

    /// <summary>UTC timestamp of the last successful authenticated request.</summary>
    public required DateTimeOffset LastActiveAt { get; set; }

    /// <summary>City-level coarse location used for proximity ranking; SRID 4326.</summary>
    public Point? Location { get; set; }

    /// <summary>Current city; null until the user picks one in onboarding.</summary>
    public Guid? CityId { get; set; }

    /// <inheritdoc cref="CityId" />
    public City? City { get; set; }

    /// <summary>0–100 score recomputed server-side on every new <see cref="MeetupReview"/>.</summary>
    public int TrustScore { get; set; }

    /// <summary>Total meetups the user has had (denormalised; updated on review submit).</summary>
    public int MeetupCount { get; set; }

    /// <summary>Number of distinct cities recorded in <see cref="Cities"/> (denormalised).</summary>
    public int CitiesCount { get; set; }

    /// <summary>Years of nomadic life — display-only stat from onboarding.</summary>
    public decimal? YearsNomading { get; set; }

    /// <summary>Photos in display order (max <see cref="WanderMeet.Shared.ValidationConstants.MaxPhotosPerUser"/>).</summary>
    public List<UserPhoto> Photos { get; set; } = [];

    /// <summary>Travel history (one row per city visit).</summary>
    public List<UserCity> Cities { get; set; } = [];

    /// <summary>Many-to-many link to selected hangout types.</summary>
    public List<UserHangoutTag> HangoutTags { get; set; } = [];

    /// <summary>Firebase Cloud Messaging device token; updated when the app registers or refreshes its FCM registration.</summary>
    public string? FcmToken { get; set; }
}
