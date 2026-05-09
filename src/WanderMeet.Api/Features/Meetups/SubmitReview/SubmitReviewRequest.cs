namespace WanderMeet.Api.Features.Meetups.SubmitReview;

/// <summary>Request body for POST /api/v1/meetups/{id}/review. The <see cref="Id"/> is bound from the route.</summary>
public record SubmitReviewRequest
{
    /// <summary>Route-bound meetup id.</summary>
    public Guid Id { get; init; }

    /// <summary>Did the meetup actually happen?</summary>
    public bool DidMeet { get; init; }

    /// <summary>Reviewer felt safe during the meetup.</summary>
    public bool FeltSafe { get; init; }

    /// <summary>Conversation was good.</summary>
    public bool GoodConvo { get; init; }

    /// <summary>Reviewer would meet the reviewee again.</summary>
    public bool WouldMeetAgain { get; init; }

    /// <summary>Optional public note; max 120 characters.</summary>
    public string? Text { get; init; }
}
