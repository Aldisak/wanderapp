namespace WanderMeet.Api.Features.Auth.Register;

/// <summary>Request body for POST /api/v1/auth/register.</summary>
public sealed class RegisterRequest
{
    /// <summary>Display name for the new user profile.</summary>
    public required string FirstName { get; init; }
}
