namespace WanderMeet.Api.Features.Invites.DeclineInvite;

/// <summary>Route parameters for PATCH /api/v1/invites/{id}/decline.</summary>
/// <param name="Id">The id of the invite to decline.</param>
public record DeclineInviteRequest(Guid Id);
