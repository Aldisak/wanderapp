using Microsoft.AspNetCore.Mvc;

namespace WanderMeet.Api.Features.Discovery.Arriving;

/// <summary>Query parameters for the arriving-soon users endpoint.</summary>
public record DiscoverArrivingRequest
{
    /// <summary>The city to check for arriving users.</summary>
    [FromQuery]
    public Guid CityId { get; init; }
}
