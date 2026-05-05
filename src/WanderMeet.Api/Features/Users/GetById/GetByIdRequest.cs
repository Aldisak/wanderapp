namespace WanderMeet.Api.Features.Users.GetById;

/// <summary>Path parameters for <c>GET /users/{id}</c>.</summary>
public record GetByIdRequest
{
    /// <summary>User id from the route.</summary>
    public Guid Id { get; init; }
}
