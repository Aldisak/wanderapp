namespace WanderMeet.Api.Database.Entities;

/// <summary>Join row between <see cref="User"/> and <see cref="HangoutTag"/>.</summary>
public class UserHangoutTag : AuditableEntity
{
    /// <summary>User side of the relationship.</summary>
    public required Guid UserId { get; set; }

    /// <inheritdoc cref="UserId" />
    public User? User { get; set; }

    /// <summary>Tag side of the relationship.</summary>
    public required Guid HangoutTagId { get; set; }

    /// <inheritdoc cref="HangoutTagId" />
    public HangoutTag? HangoutTag { get; set; }
}
