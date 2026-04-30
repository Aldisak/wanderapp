using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Database.Entities;

/// <summary>One invite from sender to receiver. Lifecycle is <see cref="Status"/>.</summary>
public class Invite : AuditableEntity
{
    /// <summary>User who sent the invite.</summary>
    public required Guid SenderId { get; set; }

    /// <inheritdoc cref="SenderId" />
    public User? Sender { get; set; }

    /// <summary>User the invite was sent to.</summary>
    public required Guid ReceiverId { get; set; }

    /// <inheritdoc cref="ReceiverId" />
    public User? Receiver { get; set; }

    /// <summary>Hangout type chosen at send time.</summary>
    public required Guid HangoutTagId { get; set; }

    /// <inheritdoc cref="HangoutTagId" />
    public HangoutTag? HangoutTag { get; set; }

    /// <summary>Suggested place for the meetup.</summary>
    public required Guid PlaceId { get; set; }

    /// <inheritdoc cref="PlaceId" />
    public Place? Place { get; set; }

    /// <summary>True when the sender used the "I'm already here" toggle.</summary>
    public bool SenderIsThere { get; set; }

    /// <summary>Current state of the invite.</summary>
    public required InviteStatus Status { get; set; }

    /// <summary>UTC timestamp the invite was sent.</summary>
    public required DateTimeOffset SentAt { get; set; }

    /// <summary>UTC timestamp the receiver accepted or declined; null while pending.</summary>
    public DateTimeOffset? RespondedAt { get; set; }

    /// <summary>UTC timestamp at which the invite auto-expires (sent_at + 48 h).</summary>
    public required DateTimeOffset ExpiresAt { get; set; }
}
