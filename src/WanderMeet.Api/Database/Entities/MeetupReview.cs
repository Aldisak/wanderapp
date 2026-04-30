namespace WanderMeet.Api.Database.Entities;

/// <summary>One side of a post-meetup review. Two reviews per meetup max (one per participant).</summary>
public class MeetupReview : AuditableEntity
{
    /// <summary>Meetup being reviewed.</summary>
    public required Guid MeetupId { get; set; }

    /// <inheritdoc cref="MeetupId" />
    public Meetup? Meetup { get; set; }

    /// <summary>User leaving the review.</summary>
    public required Guid ReviewerId { get; set; }

    /// <inheritdoc cref="ReviewerId" />
    public User? Reviewer { get; set; }

    /// <summary>User being reviewed (the other participant).</summary>
    public required Guid RevieweeId { get; set; }

    /// <inheritdoc cref="RevieweeId" />
    public User? Reviewee { get; set; }

    /// <summary>Did the meetup actually happen? Drives <see cref="Place.WanderMeetupCount"/> increment.</summary>
    public required bool DidMeet { get; set; }

    /// <summary>Reviewer felt safe.</summary>
    public bool FeltSafe { get; set; }

    /// <summary>Conversation was good.</summary>
    public bool GoodConvo { get; set; }

    /// <summary>Reviewer would meet the reviewee again.</summary>
    public bool WouldMeetAgain { get; set; }

    /// <summary>Optional public note shown on the reviewee's profile; max 120 chars.</summary>
    public string? Text { get; set; }
}
