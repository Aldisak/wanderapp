namespace WanderMeet.Api.Features.Invites.SendInvite;

/// <summary>Request body for POST /api/v1/invites (send an invite to another user).</summary>
public record SendInviteRequest
{
    /// <summary>Id of the user being invited.</summary>
    public required Guid ReceiverId { get; init; }

    /// <summary>Id of the hangout tag chosen by the sender.</summary>
    public required Guid HangoutTagId { get; init; }

    /// <summary>Id of the suggested meetup place.</summary>
    public required Guid PlaceId { get; init; }

    /// <summary>True when the sender is already at the suggested place.</summary>
    public required bool SenderIsThere { get; init; }
}
