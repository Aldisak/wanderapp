namespace WanderMeet.Api.Features.Auth.RefreshToken;

/// <summary>Response body for a successful POST /api/v1/auth/refresh.</summary>
/// <param name="AccessToken">The new access token issued by Azure AD B2C.</param>
/// <param name="RefreshToken">The new refresh token issued by Azure AD B2C.</param>
public record RefreshResponse(string AccessToken, string RefreshToken);
