namespace WanderMeet.Api.Features.Users.Shared;

/// <summary>Profile data returned by GetMe and UpdateMe endpoints.</summary>
public record UserDto(
    Guid Id,
    string FirstName,
    string? Bio,
    bool IsIdVerified,
    bool IsOpenToday,
    bool IsOpenToRomance,
    DateTimeOffset LastActiveAt,
    int TrustScore,
    int MeetupCount,
    int CitiesCount,
    decimal? YearsNomading,
    Guid? CityId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<Guid> HangoutTagIds);
