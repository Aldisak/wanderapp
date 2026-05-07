namespace WanderMeet.Api.Features.Discovery.Arriving;

/// <summary>Response payload for the arriving-soon users endpoint.</summary>
public record DiscoverArrivingResponse(IReadOnlyList<ArrivingUserDto> Items);
