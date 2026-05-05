namespace WanderMeet.Api.Features.Users.UpdateMe;

/// <summary>Request body for PATCH /api/v1/users/me. All fields are optional; only provided fields are applied.</summary>
public record UpdateMeRequest
{
    /// <summary>Optional new bio; null means "leave unchanged".</summary>
    public string? Bio { get; init; }

    /// <summary>Optional romantic-matching preference update; null means "leave unchanged".</summary>
    public bool? IsOpenToRomance { get; init; }

    /// <summary>Optional replacement set of hangout tag IDs; null means "leave unchanged".</summary>
    public IReadOnlyList<Guid>? HangoutTagIds { get; init; }
}
