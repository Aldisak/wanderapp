namespace WanderMeet.Api.Features.Users.UpdateCity;

/// <summary>Path + body for <c>PATCH /users/me/cities/{id}</c>.</summary>
public record UpdateCityRequest
{
    /// <summary>Path parameter — id of the UserCity row.</summary>
    public Guid Id { get; init; }

    /// <summary>UTC timestamp when the user left this city. <c>null</c> means still here.</summary>
    public DateTimeOffset? DepartedAt { get; init; }
}
