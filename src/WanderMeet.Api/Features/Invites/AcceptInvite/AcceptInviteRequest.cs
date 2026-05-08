namespace WanderMeet.Api.Features.Invites.AcceptInvite;

/// <summary>Route parameters for PATCH /api/v1/invites/{id}/accept.</summary>
/// <param name="Id">The id of the invite to accept.</param>
public record AcceptInviteRequest(Guid Id);
