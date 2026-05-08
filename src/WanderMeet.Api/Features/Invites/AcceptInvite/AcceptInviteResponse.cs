using WanderMeet.Api.Features.Invites.Shared;

namespace WanderMeet.Api.Features.Invites.AcceptInvite;

/// <summary>Response body for PATCH /api/v1/invites/{id}/accept.</summary>
/// <param name="Invite">The updated invite after acceptance.</param>
/// <param name="MeetupId">The id of the newly created <c>Meetup</c> row.</param>
public record AcceptInviteResponse(InviteDto Invite, Guid MeetupId);
