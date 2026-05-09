namespace WanderMeet.Api.Features.Users.UpdateFcmToken;

/// <summary>Request body for PATCH /api/v1/users/me/fcm-token.</summary>
public record UpdateFcmTokenRequest
{
    /// <summary>Firebase Cloud Messaging device registration token; max 512 chars.</summary>
    public string? Token { get; init; }
}
