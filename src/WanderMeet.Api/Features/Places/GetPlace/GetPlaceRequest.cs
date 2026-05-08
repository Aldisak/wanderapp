namespace WanderMeet.Api.Features.Places.GetPlace;

/// <summary>Route parameters for the get-place endpoint.</summary>
public record GetPlaceRequest
{
    /// <summary>The unique identifier of the place.</summary>
    public Guid Id { get; init; }
}
