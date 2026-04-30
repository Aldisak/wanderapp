namespace WanderMeet.Api.Database.Entities;

/// <summary>One profile photo. Max four per user enforced in the upload endpoint.</summary>
public class UserPhoto : AuditableEntity
{
    /// <summary>Owner.</summary>
    public required Guid UserId { get; set; }

    /// <inheritdoc cref="UserId" />
    public User? User { get; set; }

    /// <summary>Azure CDN URL of the uploaded blob.</summary>
    public required string BlobUrl { get; set; }

    /// <summary>Display order, 0–3.</summary>
    public required int Order { get; set; }
}
