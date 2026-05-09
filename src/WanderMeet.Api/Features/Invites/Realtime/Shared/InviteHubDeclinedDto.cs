namespace WanderMeet.Api.Features.Invites.Realtime.Shared;

/// <summary>
/// Payload pushed to the sender's hub connection when their invite is declined.
/// The receiver sees no UI toast — this is a silent server-side event for the sender only.
/// </summary>
/// <param name="InviteId">The declined invite primary key.</param>
public record InviteHubDeclinedDto(Guid InviteId);
