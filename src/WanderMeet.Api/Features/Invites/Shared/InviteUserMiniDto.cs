namespace WanderMeet.Api.Features.Invites.Shared;

/// <summary>
/// Minimal user profile embedded in invite responses.
/// Exposes only non-sensitive identity fields — no Azure AD B2C id, location, trust score, or email.
/// </summary>
/// <param name="Id">User primary key.</param>
/// <param name="FirstName">User's first name.</param>
/// <param name="PhotoUrl">URL of the user's first (lowest-order) non-deleted photo, or <c>null</c> if none.</param>
public record InviteUserMiniDto(Guid Id, string FirstName, string? PhotoUrl);
