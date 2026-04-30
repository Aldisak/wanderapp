using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Database.Entities;

/// <summary>Seed-data row identifying one of five canonical hangout types.</summary>
public class HangoutTag : AuditableEntity
{
    /// <summary>Stable enum-style identifier used in API contracts.</summary>
    public required HangoutTagSlug Slug { get; set; }

    /// <summary>Human-readable label for UI.</summary>
    public required string Label { get; set; }

    /// <summary>Single-emoji icon used in cards and notifications.</summary>
    public required string Emoji { get; set; }
}
