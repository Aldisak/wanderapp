namespace WanderMeet.Api.Features.Users.ToggleOpenToday;

/// <summary>Request body for PATCH /api/v1/users/me/open-today.</summary>
public record ToggleOpenTodayRequest
{
    /// <summary>The desired open-today state.</summary>
    public required bool IsOpen { get; init; }
}
