namespace WanderMeet.Api.Features.Invites.Realtime.Shared;

/// <summary>
/// Payload pushed to both sender and receiver when a pending invite expires without a response.
/// Pushed to both participants so they can remove it from their pending lists.
/// </summary>
/// <param name="InviteId">The expired invite primary key.</param>
public record InviteHubExpiredDto(Guid InviteId);
