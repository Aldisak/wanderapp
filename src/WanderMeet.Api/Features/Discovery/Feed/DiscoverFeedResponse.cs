using WanderMeet.Api.Features.Users.Shared;

namespace WanderMeet.Api.Features.Discovery.Feed;

/// <summary>Response body for GET /api/v1/discover.</summary>
public record DiscoverFeedResponse(IReadOnlyList<PublicUserDto> Items, string? NextCursor);
