namespace WanderMeet.Api.Features.Invites.Realtime.Shared;

/// <summary>
/// Payload pushed to the sender's hub connection when their invite is accepted.
/// Contains the invite id, the new meetup id, and the acceptance timestamp.
/// </summary>
/// <param name="InviteId">The accepted invite primary key.</param>
/// <param name="MeetupId">The newly created meetup row id.</param>
/// <param name="AcceptedAt">UTC timestamp when the invite was accepted.</param>
public record InviteHubAcceptedDto(Guid InviteId, Guid MeetupId, DateTimeOffset AcceptedAt);
