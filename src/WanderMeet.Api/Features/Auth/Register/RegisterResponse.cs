namespace WanderMeet.Api.Features.Auth.Register;

/// <summary>Response body for a successful POST /api/v1/auth/register.</summary>
public record RegisterResponse(
    Guid Id,
    string FirstName,
    bool IsIdVerified,
    bool IsOpenToday,
    bool IsOpenToRomance,
    int TrustScore,
    int MeetupCount,
    int CitiesCount,
    DateTimeOffset CreatedAt);
