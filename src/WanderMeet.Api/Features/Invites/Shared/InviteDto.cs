using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Features.Invites.Shared;

/// <summary>
/// Full invite representation returned by all invite endpoints.
/// Does NOT expose AzureAdB2CId, email, location, trust score, or other sensitive identity fields (spec NF7).
/// </summary>
/// <param name="Id">Invite primary key.</param>
/// <param name="Sender">Minimal profile of the user who sent the invite.</param>
/// <param name="Receiver">Minimal profile of the user the invite was sent to.</param>
/// <param name="HangoutTagId">Id of the hangout tag chosen at send time.</param>
/// <param name="HangoutTagSlug">Slug of the hangout tag (enum name as string).</param>
/// <param name="Place">Minimal info about the suggested meetup place.</param>
/// <param name="SenderIsThere">True when the sender used the "I'm already here" toggle.</param>
/// <param name="Status">Current lifecycle status of the invite.</param>
/// <param name="SentAt">UTC timestamp when the invite was sent.</param>
/// <param name="RespondedAt">UTC timestamp when the receiver accepted or declined; <c>null</c> while pending.</param>
/// <param name="ExpiresAt">UTC timestamp at which the invite auto-expires (sent + 48 h).</param>
public record InviteDto(
    Guid Id,
    InviteUserMiniDto Sender,
    InviteUserMiniDto Receiver,
    Guid HangoutTagId,
    string HangoutTagSlug,
    InvitePlaceMiniDto Place,
    bool SenderIsThere,
    InviteStatus Status,
    DateTimeOffset SentAt,
    DateTimeOffset? RespondedAt,
    DateTimeOffset ExpiresAt);
