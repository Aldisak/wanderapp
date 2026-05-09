using WanderMeet.Api.Features.Invites.Shared;

namespace WanderMeet.Api.Features.Invites.Realtime.Shared;

/// <summary>
/// Payload pushed to the receiver's hub connection when a new invite is sent to them.
/// Shape mirrors the incoming invite list DTO; PII fields (AzureAdB2CId, email, fcmToken, bio) are excluded.
/// </summary>
/// <param name="Id">Invite primary key.</param>
/// <param name="Sender">Minimal profile of the user who sent the invite.</param>
/// <param name="HangoutTagSlug">Slug of the hangout tag chosen at send time.</param>
/// <param name="Place">Minimal info about the suggested meetup place.</param>
/// <param name="SentAt">UTC timestamp when the invite was sent.</param>
/// <param name="ExpiresAt">UTC timestamp at which the invite auto-expires.</param>
public record InviteHubReceivedDto(
    Guid Id,
    InviteUserMiniDto Sender,
    string HangoutTagSlug,
    InvitePlaceMiniDto Place,
    DateTimeOffset SentAt,
    DateTimeOffset ExpiresAt);
