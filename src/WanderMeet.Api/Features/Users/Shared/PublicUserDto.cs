namespace WanderMeet.Api.Features.Users.Shared;

/// <summary>Public-facing profile fields visible to other users (no Azure AD B2C identity, no internal flags).</summary>
public record PublicUserDto(
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
