namespace WanderMeet.Api.Database.Entities;

/// <summary>Base for entities that track creation and (soft) deletion timestamps.</summary>
public abstract class AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>UTC timestamp when the row was first persisted.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>UTC timestamp when the row was last persisted, or <c>null</c> if never updated.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>UTC timestamp when the row was soft-deleted, or <c>null</c> if active.</summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
