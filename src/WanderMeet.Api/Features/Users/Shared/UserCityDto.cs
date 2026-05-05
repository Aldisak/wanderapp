namespace WanderMeet.Api.Features.Users.Shared;

/// <summary>One row of a user's travel history.</summary>
public record UserCityDto(
    Guid Id,
    Guid CityId,
    DateTimeOffset ArrivedAt,
    DateTimeOffset? DepartedAt);
