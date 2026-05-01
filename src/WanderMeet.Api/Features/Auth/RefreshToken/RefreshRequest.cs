namespace WanderMeet.Api.Features.Auth.RefreshToken;

/// <summary>Request body for POST /api/v1/auth/refresh.</summary>
public record RefreshRequest
{
    /// <summary>The refresh token issued by Azure AD B2C.</summary>
    public required string RefreshToken { get; init; }
}
