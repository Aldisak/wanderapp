namespace WanderMeet.Api.Database.Entities;

/// <summary>A confirmed meetup created when an invite is accepted (1-to-1 with <see cref="Invite"/>).</summary>
public class Meetup : AuditableEntity
{
    /// <summary>Unique back-link to the originating invite.</summary>
    public required Guid InviteId { get; set; }

    /// <inheritdoc cref="InviteId" />
    public Invite? Invite { get; set; }

    /// <summary>Sender side of the meetup.</summary>
    public required Guid UserAId { get; set; }

    /// <inheritdoc cref="UserAId" />
    public User? UserA { get; set; }

    /// <summary>Receiver side of the meetup.</summary>
    public required Guid UserBId { get; set; }

    /// <inheritdoc cref="UserBId" />
    public User? UserB { get; set; }

    /// <summary>Place the meetup occurred at.</summary>
    public required Guid PlaceId { get; set; }

    /// <inheritdoc cref="PlaceId" />
    public Place? Place { get; set; }

    /// <summary>UTC timestamp the meetup record was created (proxy for "met at" until reviewed).</summary>
    public required DateTimeOffset MetAt { get; set; }

    /// <summary>True once the 3-hour review prompt has been pushed (idempotency flag).</summary>
    public bool PromptSent { get; set; }
}
