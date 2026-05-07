using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Features.Discovery.Feed;

/// <summary>Query parameters for the discovery feed endpoint.</summary>
public record DiscoverFeedRequest
{
    /// <summary>City to discover users near.</summary>
    public Guid CityId { get; init; }

    /// <summary>Optional hangout-tag slug filter (enum name, case-insensitive).</summary>
    public string? HangoutTagSlug { get; init; }

    /// <summary>Page size (1–50, default 20).</summary>
    public int Limit { get; init; } = 20;

    /// <summary>Opaque cursor from the previous page's <c>NextCursor</c>.</summary>
    public string? Cursor { get; init; }
}
